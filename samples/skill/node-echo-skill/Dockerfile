FROM node:alpine
WORKDIR /app
COPY dist/bundle.cjs .
COPY public ./public
CMD ["node", "bundle.cjs"]