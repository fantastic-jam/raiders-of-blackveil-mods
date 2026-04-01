import tseslint from 'typescript-eslint'
import eslintConfigPrettier from 'eslint-config-prettier'

export default tseslint.config(tseslint.configs.strictTypeChecked, eslintConfigPrettier, {
  languageOptions: {
    parserOptions: { projectService: true },
  },
})
