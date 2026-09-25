# Adaptive Cards and dialogs

Primary sources:

- [Adaptive Card reference](https://learn.microsoft.com/en-us/microsoftteams/platform/task-modules-and-cards/cards/cards-reference#adaptive-card)
- [Dialogs](https://learn.microsoft.com/en-us/microsoftteams/platform/task-modules-and-cards/what-are-task-modules)
- [Invoke dialogs from a bot](https://learn.microsoft.com/en-us/microsoftteams/platform/task-modules-and-cards/task-modules/invoking-task-modules)

## Adaptive Cards

Sending an Adaptive Card normally requires no dedicated manifest property. Card JSON and activity handlers define the behavior.

Card features such as `Action.Execute`, card refresh, people picker, typeahead search, and card-based dialog content do not by themselves justify a new top-level manifest capability. Check their feature documentation for authentication, domain, or version prerequisites.

Do not confuse these cases:

- A card button that sends an invoke activity: code-only routing plus the base bot declaration.
- A card button that opens an Adaptive Card dialog: no URL domain is required for the card content.
- A card button that opens a URL dialog: the URL domain must be declared in `validDomains`.
- A card returned from a message extension: the message extension still requires `composeExtensions`.

## Dialog detection

Strong evidence includes task/dialog fetch and submit routes, `TaskFetchAction`, or responses containing task/dialog information.

Classify the content:

| Dialog content | Manifest impact |
|---|---|
| Adaptive Card | No additional dialog-specific field |
| HTTPS URL | Add the exact host to `validDomains` |
| Message-extension action | Define the matching `composeExtensions.commands` action command |
| Tab-initiated dialog | Evaluate the tab declaration and its domains |

For URL dialogs, extract only the host. Do not place paths, query strings, schemes, or unowned wildcard domains in `validDomains`.

If the URL is assembled from configuration and no public value exists, report the domain as `needs-input`. Do not substitute `localhost` or an example production domain.

## Evidence output

Record each dialog route, trigger source, content type, URL host if applicable, and the matching manifest decision. One sample can use both card and URL dialogs and therefore require different decisions per route.
