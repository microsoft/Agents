import neostandard from 'neostandard'

export default [
  ...neostandard({ ts: true, ignores: ['node_modules', '**/dist/**'] }),
  {
    files: ['automation/teams-sample-sync/**/*.{ts,mjs}'],
    rules: {
      curly: ['error', 'all'],
      '@stylistic/brace-style': ['error', '1tbs', { allowSingleLine: false }],
      '@stylistic/object-curly-newline': ['error', { multiline: true, minProperties: 4 }],
      '@stylistic/object-property-newline': ['error', { allowAllPropertiesOnSameLine: false }],
      '@stylistic/member-delimiter-style': ['error', {
        multiline: { delimiter: 'none' },
        singleline: { delimiter: 'semi', requireLast: false },
      }],
      '@stylistic/padding-line-between-statements': [
        'error',
        { blankLine: 'always', prev: '*', next: 'return' },
      ],
    },
  },
  {
    files: ['automation/teams-sample-sync/src/**/*.ts'],
    rules: {
      '@stylistic/padding-line-between-statements': [
        'error',
        { blankLine: 'always', prev: '*', next: 'return' },
        { blankLine: 'always', prev: '*', next: ['if', 'for', 'while', 'try'] },
        { blankLine: 'always', prev: 'block-like', next: '*' },
      ],
    },
  },
]
