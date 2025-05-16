// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
import { Attachment, Channels } from '@microsoft/agents-activity'
import { MessageFactory, AgentStatePropertyAccessor, TurnContext, UserState } from '@microsoft/agents-hosting'
import {
  AttachmentPrompt,
  ChoiceFactory,
  ChoicePrompt,
  ComponentDialog,
  ConfirmPrompt,
  DialogSet,
  DialogTurnStatus,
  NumberPrompt,
  PromptValidatorContext,
  TextPrompt,
  WaterfallDialog,
  WaterfallStepContext
} from '@microsoft/agents-hosting-dialogs'
import { UserProfile } from './userProfile'

const ATTACHMENT_PROMPT = 'ATTACHMENT_PROMPT'
const CHOICE_PROMPT = 'CHOICE_PROMPT'
const CONFIRM_PROMPT = 'CONFIRM_PROMPT'
const NAME_PROMPT = 'NAME_PROMPT'
const NUMBER_PROMPT = 'NUMBER_PROMPT'
const USER_PROFILE = 'USER_PROFILE'
const WATERFALL_DIALOG = 'WATERFALL_DIALOG'

export class UserProfileDialog extends ComponentDialog {
  private userProfile: AgentStatePropertyAccessor<UserProfile>

  constructor (userState: UserState) {
    super('userProfileDialog')

    this.userProfile = userState.createProperty(USER_PROFILE)

    this.addDialog(new TextPrompt(NAME_PROMPT))
    this.addDialog(new ChoicePrompt(CHOICE_PROMPT))
    this.addDialog(new ConfirmPrompt(CONFIRM_PROMPT))
    this.addDialog(new NumberPrompt(NUMBER_PROMPT, this.agePromptValidator))
    this.addDialog(new AttachmentPrompt(ATTACHMENT_PROMPT, this.picturePromptValidator))

    this.addDialog(new WaterfallDialog(WATERFALL_DIALOG, [
      this.transportStep.bind(this),
      this.nameStep.bind(this),
      this.nameConfirmStep.bind(this),
      this.ageStep.bind(this),
      this.pictureStep.bind(this),
      this.summaryStep.bind(this),
      this.confirmStep.bind(this)
    ]))

    this.initialDialogId = WATERFALL_DIALOG
  }

  /**
     * The run method handles the incoming activity (in the form of a TurnContext) and passes it through the dialog system.
     * If no dialog is active, it will start the default dialog.
     * @param {*} turnContext
     * @param {*} accessor
     */
  public async run (turnContext: TurnContext, accessor: AgentStatePropertyAccessor) {
    const dialogSet = new DialogSet(accessor)
    dialogSet.add(this)

    const dialogContext = await dialogSet.createContext(turnContext)
    const results = await dialogContext.continueDialog()
    if (results.status === DialogTurnStatus.empty) {
      await dialogContext.beginDialog(this.id)
    }
  }

  private async transportStep (stepContext: WaterfallStepContext) {
    // WaterfallStep always finishes with the end of the Waterfall or with another dialog; here it is a Prompt Dialog.
    // Running a prompt here means the next WaterfallStep will be run when the users response is received.
    return await stepContext.prompt(CHOICE_PROMPT, {
      choices: ChoiceFactory.toChoices(['Car', 'Bus', 'Bicycle']),
      prompt: 'Please enter your mode of transport.'
    })
  }

  private async nameStep (stepContext: WaterfallStepContext<UserProfile>) {
    stepContext.options.transport = stepContext.result.value
    return await stepContext.prompt(NAME_PROMPT, 'What is your name, human?')
  }

  private async nameConfirmStep (stepContext: WaterfallStepContext<UserProfile>) {
    stepContext.options.name = stepContext.result

    // We can send messages to the user at any point in the WaterfallStep.
    await stepContext.context.sendActivity(`Thanks ${stepContext.result}.`)

    // WaterfallStep always finishes with the end of the Waterfall or with another dialog; here it is a Prompt Dialog.
    return await stepContext.prompt(CONFIRM_PROMPT, 'Do you want to give your age?', ['yes', 'no'])
  }

  private async ageStep (stepContext: WaterfallStepContext) {
    if (stepContext.result === true) {
      // User said "yes" so we will be prompting for the age.
      // WaterfallStep always finishes with the end of the Waterfall or with another dialog, here it is a Prompt Dialog.
      const promptOptions = { prompt: 'Please enter your age.', retryPrompt: 'The value entered must be greater than 0 and less than 150.' }

      return await stepContext.prompt(NUMBER_PROMPT, promptOptions)
    } else {
      // User said "no" so we will skip the next step. Give -1 as the age.
      return await stepContext.next(-1)
    }
  }

  async pictureStep (stepContext: WaterfallStepContext<UserProfile>) {
    stepContext.options.age = stepContext.result

    const msg = stepContext.options.age === -1 ? 'No age given.' : `I have your age as ${stepContext.options.age}.`

    // We can send messages to the user at any point in the WaterfallStep.
    await stepContext.context.sendActivity(msg)

    if (stepContext.context.activity.channelId === Channels.Msteams) {
      // This attachment prompt example is not designed to work for Teams attachments, so skip it in this case
      await stepContext.context.sendActivity('Skipping attachment prompt in Teams channel...')
      return await stepContext.next(undefined)
    } else {
      // WaterfallStep always finishes with the end of the Waterfall or with another dialog; here it is a Prompt Dialog.
      const promptOptions = {
        prompt: 'Please attach a profile picture (or type any message to skip).',
        retryPrompt: 'The attachment must be a jpeg/png image file.'
      }

      return await stepContext.prompt(ATTACHMENT_PROMPT, promptOptions)
    }
  }

  private async confirmStep (stepContext: WaterfallStepContext<UserProfile>) {
    let msg = 'Thanks.'
    if (stepContext.result) {
      msg += ' Your profile saved successfully.'
    } else {
      msg += ' Your profile will not be kept.'
    }

    await stepContext.context.sendActivity(msg)

    // WaterfallStep always finishes with the end of the Waterfall or with another dialog; here it is a Prompt Dialog.
    return await stepContext.endDialog()
  }

  private async summaryStep (stepContext: WaterfallStepContext<UserProfile>) {
    // Get the current profile object from user state.
    stepContext.options.picture = stepContext.result && stepContext.result[0]
    const userProfile = await this.userProfile.get(stepContext.context, new UserProfile())
    const stepContextOptions = stepContext.options
    userProfile.transport = stepContextOptions.transport
    userProfile.name = stepContextOptions.name
    userProfile.age = stepContextOptions.age
    userProfile.picture = stepContext.options.picture

    let msg = `I have your mode of transport as ${userProfile.transport} and your name as ${userProfile.name}.`
    if (userProfile.age !== -1) {
      msg += ` And age as ${userProfile.age}.`
    }

    msg += '.'
    await stepContext.context.sendActivity(msg)
    if (userProfile.picture) {
      try {
        await stepContext.context.sendActivity(MessageFactory.attachment(userProfile.picture, 'This is your profile picture.'))
      } catch {
        await stepContext.context.sendActivity('A profile picture was saved but could not be displayed here.')
      }
    }
    // WaterfallStep always finishes with the end of the Waterfall or with another dialog; here it is the end.
    return await stepContext.prompt(CONFIRM_PROMPT, { prompt: 'Is this okay?' })
  }

  private async agePromptValidator (promptContext: PromptValidatorContext<number>) {
    const value = promptContext.recognized.succeeded ? promptContext.recognized.value : undefined
    return value !== undefined && value > 0 && value < 150
  }

  private async picturePromptValidator (promptContext: PromptValidatorContext<Attachment[]>) {
    if (promptContext.recognized.succeeded) {
      const attachments = promptContext.recognized.value ?? []
      const validImages: Attachment[] = []

      attachments.forEach(attachment => {
        if (attachment.contentType === 'image/jpeg' || attachment.contentType === 'image/png') {
          validImages.push(attachment)
        }
      })

      promptContext.recognized.value = validImages

      // If none of the attachments are valid images, the retry prompt should be sent.
      return !!validImages.length
    } else {
      await promptContext.context.sendActivity('No attachments received. Proceeding without a profile picture...')

      // We can return true from a validator function even if Recognized.Succeeded is false.
      return true
    }
  }
}
