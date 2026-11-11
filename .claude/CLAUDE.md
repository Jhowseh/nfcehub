# Regras dos projetos em C:\PBtec\App PBtec

## Comentários
- Nunca escreva comentários no código: nada de `#`, docstrings (`"""..."""`), `# noqa`, `# type:` ou similares.
- Toda informação que explicaria o código (por que algo é feito assim, como o SysPDV guarda os dados, detalhes do Windows, limitações) vai para a documentação do projeto: `README.md` e a pasta `docs/`.
- Ao mudar o comportamento do código, atualize a documentação na mesma alteração.

## Idioma
- Código em inglês: nomes de arquivos, pastas de código, módulos, classes, funções, variáveis, constantes, chaves de configuração (`config.ini`) e mensagens de log.
- Em português do Brasil: tudo o que o cliente vê (textos das janelas, mensagens de erro mostradas na tela, relatórios gravados, nomes de pastas criadas para o cliente, `LEIA-ME.txt`) e a documentação do projeto (`README.md`, `docs/`).

## Build
- A saída da compilação fica na pasta `dist/` de cada projeto.
