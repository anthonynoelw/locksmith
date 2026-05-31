module.exports = {
  extends: ['@commitlint/config-conventional'],
  rules: {
    'type-enum': [2, 'always', [
      'feat',      // A new feature
      'fix',       // A bug fix
      'docs',      // Documentation only changes
      'style',     // Changes that don't affect code meaning
      'refactor',  // Code change that neither fixes a bug nor adds a feature
      'perf',      // Code change that improves performance
      'test',      // Adding missing tests or correcting existing tests
      'chore',     // Changes to build process, dependencies, etc.
      'ci',        // Changes to CI configuration files
    ]],
    'type-case': [2, 'always', 'lower-case'],
    'type-empty': [2, 'never'],
    'scope-case': [2, 'always', 'lower-case'],
    'subject-empty': [2, 'never'],
    'subject-full-stop': [2, 'never', '.'],
  },
};