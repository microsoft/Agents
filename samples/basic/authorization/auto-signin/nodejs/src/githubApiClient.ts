export interface PullRequest {
  id: number
  title: string
  url: string
}

export const getCurrentProfile = async (token: string) : Promise<any> => {
  const url = 'https://api.github.com/user'
  const headers = {
    Authorization: `Bearer ${token}`,
    Accept: 'application/vnd.github.v3+json',
    'User-Agent': 'AgentsSDKDemo' // Replace with your app name
  }
  const response = await fetch(url, { headers })
  if (!response.ok) {
    throw new Error(`Error fetching pull requests: ${response.statusText}`)
  }
  const data = await response.json()
  return {
    $root: {
      displayName: data.name || '',
      mail: data.html_url || '',
      jobTitle: '',
      givenName: data.login || '',
      surname: '',
      imageUri: data.avatar_url || '',
    }
  }
}

export const getPullRequests = async (owner: string, repo: string, token: string) : Promise<PullRequest[]> => {
  const url = `https://api.github.com/repos/${owner}/${repo}/pulls`
  const headers = {
    Authorization: `Bearer ${token}`,
    Accept: 'application/vnd.github.v3+json',
    'User-Agent': 'AgentsSDKDemo' // Replace with your app name
  }
  const response = await fetch(url, { headers })
  if (!response.ok) {
    throw new Error(`Error fetching pull requests: ${response.statusText}`)
  }
  const data = await response.json()
  return data as PullRequest[]
}
