# node-echo-skill

## Overview 

Bot created with the Agents SDK for JS to be integrated with Copilot Studio as a Skill.

## Debug Locally

To debug this bot you can use 

## Build and Deploy

The bot is coded with JavaScript ES6, compiled to a bundle with `esbuild` and distributed as a docker image.

### Build 

```sh
npm install
npm run docker
```

These commands Will create a docker image named `node-echo-bot:latest` that you can use to push to any container registry before deploying.

> Note: You need to have the docker daemon running to be able to produce the docker image

### Configure as a Skill

Create an Agent in Copilot Studio and configure a Topic to invoke a skill.

