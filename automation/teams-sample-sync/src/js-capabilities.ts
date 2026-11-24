/** Reads the explicit Agents SDK route registrations used by JS samples. */
import ts from 'typescript-parser'
import { sourceFiles } from './source-files.js'
import type { SourceCapabilities } from './language-validation.js'

const MESSAGE_EXTENSION_METHODS = new Set([
  'onQuery', 'onFetchAction', 'onSubmitAction', 'onQueryLink', 'onAnonymousQueryLink',
])
const COMMAND_METHODS = new Set(['onQuery', 'onFetchAction', 'onSubmitAction'])
const LEGACY_RUNTIME = /^(?:botbuilder|botframework-|@microsoft\/teams\.(?:apps|plugins)|@microsoft\/teamsfx)/

function constructsClass (node: ts.Node, classNames: Set<string>): boolean {
  return ts.isNewExpression(node) && ts.isIdentifier(node.expression) && classNames.has(node.expression.text)
}

/** One source file owns its import names, constants and lexical alias lookup. */
class SourceInspector {
  private readonly source: ts.SourceFile
  private readonly checker: ts.TypeChecker
  private readonly agentClasses = new Set<string>()
  private readonly extensionClasses = new Set<string>()
  private readonly extensions = new Set<string>()
  private readonly constants = new Map<string, string>()

  constructor (private readonly file: string, private readonly facts: SourceCapabilities) {
    // Symbols resolve lexical aliases without loading dependencies or ambient types.
    const program = ts.createProgram([file], {
      noResolve: true,
      noLib: true,
      types: [],
      allowJs: true,
    })
    this.source = program.getSourceFile(file)!
    this.checker = program.getTypeChecker()
  }

  inspect (): void {
    this.inspectImports()
    this.inspectDeclarations(this.source)
    this.inspectExtensionCallbacks(this.source)
    this.inspectRoutes(this.source)
  }

  private inspectImports (): void {
    for (const statement of this.source.statements) {
      if (!ts.isImportDeclaration(statement) || !ts.isStringLiteral(statement.moduleSpecifier)) {
        continue
      }

      const specifier = statement.moduleSpecifier.text

      if (LEGACY_RUNTIME.test(specifier)) {
        this.facts.errors!.push(`Legacy application import remains in ${this.file}: ${specifier}`)
      }

      const imports = statement.importClause?.namedBindings

      if (!imports || !ts.isNamedImports(imports)) {
        continue
      }

      for (const item of imports.elements) {
        const importedName = (item.propertyName ?? item.name).text

        if (specifier === '@microsoft/agents-hosting' && importedName === 'AgentApplication') {
          this.agentClasses.add(item.name.text)
        }

        if (specifier === '@microsoft/agents-hosting-extensions-msteams' && importedName === 'TeamsAgentExtension') {
          this.extensionClasses.add(item.name.text)
        }
      }
    }
  }

  private inspectDeclarations (node: ts.Node): void {
    if (constructsClass(node, this.agentClasses)) {
      this.facts.bot = true
    }

    if (ts.isVariableDeclaration(node) && ts.isIdentifier(node.name) && node.initializer) {
      if (ts.isStringLiteral(node.initializer)) {
        this.constants.set(node.name.text, node.initializer.text)
      }

      if (constructsClass(node.initializer, this.extensionClasses)) {
        this.extensions.add(node.name.text)
      }
    }

    if (ts.isParameter(node) && ts.isIdentifier(node.name) && node.type && ts.isTypeReferenceNode(node.type)) {
      const typeName = node.type.typeName

      if (ts.isIdentifier(typeName) && this.extensionClasses.has(typeName.text)) {
        this.extensions.add(node.name.text)
      }
    }

    ts.forEachChild(node, (child) => this.inspectDeclarations(child))
  }

  private inspectExtensionCallbacks (node: ts.Node): void {
    if (ts.isCallExpression(node) && ts.isPropertyAccessExpression(node.expression) && node.expression.name.text === 'registerExtension') {
      const extension = node.arguments[0]
      const callback = node.arguments[1]
      const knownExtension = extension && ts.isIdentifier(extension) && this.extensions.has(extension.text)
      const inlineCallback = callback && (ts.isArrowFunction(callback) || ts.isFunctionExpression(callback))

      if (knownExtension && inlineCallback) {
        const name = callback.parameters[0]?.name

        if (name && ts.isIdentifier(name)) {
          this.extensions.add(name.text)
        }
      }
    }

    ts.forEachChild(node, (child) => this.inspectExtensionCallbacks(child))
  }

  private isExtensionReceiver (node: ts.Expression, helper: string, seen = new Set<ts.Symbol>()): boolean {
    if (ts.isParenthesizedExpression(node) || ts.isAsExpression(node) || ts.isNonNullExpression(node)) {
      return this.isExtensionReceiver(node.expression, helper, seen)
    }

    if (ts.isIdentifier(node)) {
      const symbol = this.checker.getSymbolAtLocation(node)

      if (!symbol || seen.has(symbol)) {
        return false
      }

      seen.add(symbol)
      const declaration = symbol.valueDeclaration

      if (declaration && ts.isVariableDeclaration(declaration) && declaration.initializer) {
        const isConst = ts.isVariableDeclarationList(declaration.parent) && (declaration.parent.flags & ts.NodeFlags.Const)

        if (isConst) {
          return this.isExtensionReceiver(declaration.initializer, helper, seen)
        }
      }
    }

    if (ts.isCallExpression(node) && ts.isPropertyAccessExpression(node.expression)) {
      return this.isExtensionReceiver(node.expression.expression, helper, seen)
    }

    return ts.isPropertyAccessExpression(node) && ts.isIdentifier(node.expression) &&
      this.extensions.has(node.expression.text) && node.name.text === helper
  }

  private inspectUnsupportedRoutes (node: ts.Node): void {
    if (ts.isPropertyAccessExpression(node) && this.isExtensionReceiver(node.expression, 'messageExtensions')) {
      const isRouteMethod = MESSAGE_EXTENSION_METHODS.has(node.name.text)
      const calledDirectly = ts.isCallExpression(node.parent) && node.parent.expression === node

      if (isRouteMethod && !calledDirectly) {
        this.facts.errors!.push(`Detached message-extension methods are unsupported in ${this.file}: ${node.getText(this.source)}`)
      }
    }

    if (ts.isVariableDeclaration(node) && ts.isObjectBindingPattern(node.name) && node.initializer &&
      this.isExtensionReceiver(node.initializer, 'messageExtensions')) {
      this.facts.errors!.push(`Destructured message-extension methods are unsupported in ${this.file}`)
    }

    if (ts.isElementAccessExpression(node) && this.isExtensionReceiver(node.expression, 'messageExtensions')) {
      this.facts.errors!.push(`Computed message-extension methods are unsupported in ${this.file}`)
    }
  }

  private inspectRoutes (node: ts.Node): void {
    // Visit children first so chained registrations retain their registration order.
    ts.forEachChild(node, (child) => this.inspectRoutes(child))
    this.inspectUnsupportedRoutes(node)

    if (!ts.isCallExpression(node) || !ts.isPropertyAccessExpression(node.expression)) {
      return
    }

    const method = node.expression.name.text
    const receiver = node.expression.expression
    const isMessageExtension = this.isExtensionReceiver(receiver, 'messageExtensions')

    if (MESSAGE_EXTENSION_METHODS.has(method) && !isMessageExtension) {
      this.facts.errors!.push(`Unsupported message-extension route receiver in ${this.file}: ${node.expression.getText(this.source)}`)
    }

    if (this.isExtensionReceiver(receiver, 'fileConsent')) {
      this.facts.files = true
    }

    if (this.isExtensionReceiver(receiver, 'meetings')) {
      this.inspectMeetingRoute(method)
    }

    if (isMessageExtension) {
      this.inspectMessageExtensionRoute(node, method)
    }
  }

  private inspectMeetingRoute (method: string): void {
    const permissions = new Set(this.facts.resourceSpecificPermissions)

    if (method === 'onStart' || method === 'onEnd') {
      permissions.add('OnlineMeeting.ReadBasic.Chat')
      permissions.add('ChannelMeeting.ReadBasic.Group')
    }

    if (method === 'onParticipantsJoin' || method === 'onParticipantsLeave') {
      permissions.add('OnlineMeetingParticipant.Read.Chat')
    }

    this.facts.resourceSpecificPermissions = [...permissions]
  }

  private inspectMessageExtensionRoute (node: ts.CallExpression, method: string): void {
    this.facts.composeExtensions = true

    if (COMMAND_METHODS.has(method)) {
      const id = this.commandId(node.arguments[0])

      if (!id) {
        this.facts.errors!.push(`Message-extension command must have an inspectable literal ID in ${this.file}`)
      } else {
        const commands = method === 'onQuery' ? this.facts.queryCommands : this.facts.actionCommands!
        commands.push({
          id,
          evidence: `Message-extension ${method} "${id}"`
        })
      }
    }

    if (method === 'onQueryLink' || method === 'onAnonymousQueryLink') {
      this.facts.linkHandlerEvidence = 'Message-extension link handler'
    }
  }

  private commandId (argument: ts.Expression | undefined): string | undefined {
    if (argument && ts.isStringLiteral(argument)) {
      return argument.text
    }

    if (argument && ts.isIdentifier(argument)) {
      return this.constants.get(argument.text)
    }

    return undefined
  }
}

export function jsCapabilities (root: string): SourceCapabilities {
  const facts: SourceCapabilities = {
    bot: false,
    composeExtensions: false,
    queryCommands: [],
    actionCommands: [],
    errors: [],
  }
  const files = sourceFiles(root).filter((name) => /\.[cm]?[jt]s$/.test(name) && !/[\\/]tests[\\/]/.test(name))

  for (const file of files) {
    new SourceInspector(file, facts).inspect()
  }

  if (!facts.bot) {
    facts.errors!.push('Missing inspectable AgentApplication construction in JS sample source')
  }

  return facts
}
