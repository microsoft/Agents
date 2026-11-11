// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
export interface Article {
  title: string
  snippet: string
}

export async function searchWikipedia (query: string): Promise<Article[]> {
  if (!query) {
    return []
  }
  const parameters = new URLSearchParams({
    action: 'query', format: 'json', list: 'search', srsearch: query, utf8: '1', srlimit: '8',
  })
  try {
    const response = await fetch(`https://en.wikipedia.org/w/api.php?${parameters}`, {
      headers: { 'User-Agent': 'AgentMessageExtensions/1.0 (Teams Agent; +https://example.com)' },
      signal: AbortSignal.timeout(10_000),
    })
    if (!response.ok) {
      throw new Error(`Wikipedia returned HTTP ${response.status}`)
    }
    const body = await response.json() as { query?: { search?: Article[] } }
    return body.query?.search ?? []
  } catch (error) {
    console.error('Wikipedia search failed:', error)
    return []
  }
}
