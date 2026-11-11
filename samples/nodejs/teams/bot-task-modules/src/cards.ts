// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
function attachment (body: object[], actions: object[], version = '1.4') {
  return { contentType: 'application/vnd.microsoft.card.adaptive', content: { type: 'AdaptiveCard', version, body, actions } }
}

export function launcherCard () {
  return attachment([
    { type: 'TextBlock', text: 'Task Module Invocation from Adaptive Card', weight: 'Bolder', size: 'Large' },
  ], ['AdaptiveCard', 'CustomForm', 'MultiStep'].map((data, index) => ({
    type: 'Action.Submit',
    title: ['Adaptive Card', 'Custom Form', 'Multi-step Form'][index],
    data: { msteams: { type: 'task/fetch' }, data },
  })))
}

export function textInputCard () {
  return attachment([
    { type: 'TextBlock', text: 'Enter Text Here', weight: 'Bolder' },
    { type: 'Input.Text', id: 'usertext', placeholder: 'add some text and submit', isMultiline: true },
  ], [{ type: 'Action.Submit', title: 'Submit' }], '1.0')
}

export function nameCard () {
  return attachment([
    { type: 'TextBlock', text: 'Step 1 of 2 - Your Name', size: 'Large', weight: 'Bolder' },
    { type: 'Input.Text', id: 'name', label: 'Name', placeholder: 'Enter your name', isRequired: true },
  ], [{ type: 'Action.Submit', title: 'Next', data: { submissiontype: 'multi_step_1' } }])
}

export function emailCard (name: string) {
  return attachment([
    { type: 'TextBlock', text: 'Step 2 of 2 - Your Email', size: 'Large', weight: 'Bolder' },
    { type: 'Input.Text', id: 'email', label: 'Email', placeholder: 'Enter your email', isRequired: true },
  ], [{ type: 'Action.Submit', title: 'Submit', data: { submissiontype: 'multi_step_2', name } }])
}
