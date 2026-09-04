// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

export type RetrievalStatus = 'success' | 'notSignedIn' | 'noResults' | 'serviceUnavailable'

export type RetrievalItem = {
  title: string
  extract: string
  webUrl: string
}

export type RetrievalResult = {
  status: RetrievalStatus
  items: RetrievalItem[]
}

export type RetrievalOptions = {
  sharePointSiteUrl: string
  maximumNumberOfResults: number
}

type FetchFunction = (input: string, init?: RequestInit) => Promise<Response>
type GetAccessToken = () => Promise<string | undefined>
type RetrievalResponse = {
  retrievalHits?: Array<{
    webUrl?: string
    extracts?: Array<{ text?: string }>
    resourceMetadata?: { title?: string }
  }>
}

export function getRetrievalOptions (environment = process.env): RetrievalOptions {
  const sharePointSiteUrl = environment.RETRIEVAL_SHAREPOINT_SITE_URL ?? ''
  let url: URL
  try {
    url = new URL(sharePointSiteUrl)
  } catch {
    throw new Error('RETRIEVAL_SHAREPOINT_SITE_URL must be an absolute HTTPS SharePoint site URL.')
  }
  if (url.protocol !== 'https:' || !url.hostname.endsWith('.sharepoint.com')) throw new Error('RETRIEVAL_SHAREPOINT_SITE_URL must be an absolute HTTPS SharePoint site URL.')

  const maximumNumberOfResults = Number(environment.RETRIEVAL_MAXIMUM_NUMBER_OF_RESULTS ?? '3')
  if (!Number.isInteger(maximumNumberOfResults) || maximumNumberOfResults < 1 || maximumNumberOfResults > 25) {
    throw new Error('RETRIEVAL_MAXIMUM_NUMBER_OF_RESULTS must be from 1 through 25.')
  }

  return { sharePointSiteUrl, maximumNumberOfResults }
}

export function createFilterExpression (sharePointSiteUrl: string): string {
  return `path:"${sharePointSiteUrl.replace(/\/+$/, '')}/"`
}

export async function retrieveSharePoint (
  question: string,
  getAccessToken: GetAccessToken,
  options: RetrievalOptions = getRetrievalOptions(),
  fetchFunction: FetchFunction = fetch
): Promise<RetrievalResult> {
  if (!question.trim()) return { status: 'noResults', items: [] }

  let accessToken: string | undefined
  try {
    accessToken = await getAccessToken()
  } catch {
    console.warn('The delegated Microsoft Graph token was not available.')
    return { status: 'notSignedIn', items: [] }
  }

  if (!accessToken) return { status: 'notSignedIn', items: [] }

  try {
    const response = await fetchFunction('https://graph.microsoft.com/v1.0/copilot/retrieval', {
      method: 'POST',
      headers: { Authorization: `Bearer ${accessToken}`, 'Content-Type': 'application/json' },
      body: JSON.stringify({
        queryString: question,
        // SharePoint is the default data source. See the README for the OneDrive for Business fallback.
        dataSource: 'sharePoint',
        filterExpression: createFilterExpression(options.sharePointSiteUrl),
        resourceMetadata: ['title', 'author'],
        maximumNumberOfResults: options.maximumNumberOfResults
      })
    })

    if (!response.ok) {
      console.warn(`Copilot Retrieval API returned status code ${response.status}.`)
      return { status: 'serviceUnavailable', items: [] }
    }

    const payload = await response.json() as RetrievalResponse
    const items = mapRetrievalItems(payload)
    return items.length === 0 ? { status: 'noResults', items: [] } : { status: 'success', items }
  } catch {
    console.error('Copilot Retrieval API request failed.')
    return { status: 'serviceUnavailable', items: [] }
  }
}

export function mapRetrievalItems (payload: RetrievalResponse): RetrievalItem[] {
  return (payload.retrievalHits ?? []).flatMap((hit) => {
    const extract = hit.extracts?.find(item => item.text?.trim())?.text?.trim()
    if (!hit.webUrl || !extract) return []
    return [{ title: hit.resourceMetadata?.title?.trim() || 'Build session information', extract, webUrl: hit.webUrl }]
  })
}
