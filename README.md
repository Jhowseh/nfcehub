# NFCeHub

Aplicativo desktop para Windows que exporta os arquivos XML das NFC-e diretamente do banco de dados do SysPDV, para envio à contabilidade ou arquivamento. O acesso ao banco é exclusivamente de leitura: a conexão é aberta em uma transação somente leitura e nenhum dado é gravado.

O usuário informa a série e os números das notas (uma nota, um intervalo como "122 a 150" ou vários intervalos) ou importa a relação de notas a partir de um arquivo PDF, TXT ou CSV. Em seguida, seleciona a pasta de destino e clica em **Gerar XMLs**.

Desenvolvido em C# com WPF sobre o **.NET Framework 4.8**. É compatível com Windows 7 SP1 até Windows 11 (no Windows 10 e 11, o .NET Framework 4.8 já vem instalado) e é distribuído como um executável único, `NFCeHub.exe`, de aproximadamente 3,6 MB.

- Manual do usuário: [LEIA-ME.txt](LEIA-ME.txt)
- Documentação técnica: [docs/funcionamento.md](docs/funcionamento.md)

## Notas autorizadas que saem como "sem autorização"

Algumas NFC-e com situação `AU` no SysPDV aparecem como **sem autorização**, com o detalhe "AUTORIZADO O USO DA NF-E". Isso é um defeito do SysPDV, não do NFCeHub.

Acontece quando o caixa entra em contingência logo depois de a SEFAZ já ter autorizado a nota em emissão normal. O SysPDV guarda a versão de contingência (outra chave de acesso) junto com o protocolo da versão autorizada, e o XML fica inválido. A versão autorizada não é guardada em lugar nenhum: nem no banco, nem na pasta `C:\Syspdv\NFCe\Vendas`, que só tem a mesma nota de contingência, sem protocolo.

O programa não exporta esse XML, porque a contabilidade o recusaria. Na SEFAZ, a nota está autorizada com a chave que aparece em `TRANSACAO.TRNNFCECHVNFE`. Detalhes, exemplo e frequência em [docs/funcionamento.md](docs/funcionamento.md#notas-au-com-o-xml-errado-defeito-do-syspdv-na-contingência).

## Estrutura

```
NFCeHub/
├── src/
│   ├── NFCeHub.Core/                regras do programa (sem interface)
│   │   ├── Config/
│   │   │   ├── AppSettings.cs       config.ini tipado, valores do registro do SysPDV
│   │   │   ├── IniFile.cs           leitura e gravação de arquivos .ini
│   │   │   └── AppPaths.cs          pastas do programa, do config.ini e do log
│   │   ├── Data/
│   │   │   ├── Database.cs          conexão somente leitura com o Firebird
│   │   │   └── SysPdv.cs            todo o SQL das tabelas do SysPDV
│   │   ├── Export/
│   │   │   ├── Exporter.cs          decide o que gravar para cada nota
│   │   │   ├── XmlFiles.cs          descompacta, identifica e grava os XMLs
│   │   │   ├── ExportResults.cs     situações das notas e resumo da exportação
│   │   │   └── ExportReport.cs      relatório das notas sem XML autorizado
│   │   ├── Import/
│   │   │   ├── ListReader.cs        acha as notas no texto de um PDF/TXT/CSV
│   │   │   ├── DocumentText.cs      extrai o texto do PDF ou do arquivo de texto
│   │   │   └── Reading.cs           resultado da leitura
│   │   ├── NoteRange.cs             faixas de série e número
│   │   └── Log.cs                   arquivo de log
│   ├── NFCeHub/                     programa de janela (WPF) → NFCeHub.exe
│   │   ├── Windows/                 janela principal, configuração, caixas de mensagem
│   │   ├── Controls/                cabeçalho e ícones
│   │   ├── Themes/                  cores claro/escuro, estilos, ícones, logo
│   │   ├── Interop/                 chamadas do Windows (escolher pasta, trazer janela para frente)
│   │   ├── Assets/                  logos e ícone
│   │   ├── StatusLabels.cs          textos e cores de cada situação, formatação de números
│   │   └── config.example.ini       modelo do config.ini
│   └── NFCeHub.Cli/                 linha de comando para testes → nfcehub-cli.exe
├── tests/
│   └── NFCeHub.Core.Tests/          testes automáticos do núcleo (xUnit)
├── docs/                            documentação técnica
├── dist/                            saída do build (fora do Git)
├── .claude/                         regras do projeto (CLAUDE.md) e skills do Claude Code
├── .github/workflows/build.yml      compilação e testes no GitHub
├── Directory.Build.props            configurações comuns aos projetos (versão, .NET, C#)
├── build.ps1                        compila, testa e monta a dist/
├── NFCeHub.sln
└── LEIA-ME.txt                      manual do cliente
```

Cada projeto usa um namespace só (`NFCeHub.Core`, `NFCeHub`, `NFCeHub.Cli`). As subpastas servem só para organizar os arquivos.

## Requisitos para compilar

- SDK do .NET 10 (compila também para .NET Framework 4.8). Instalado com:

```bash
winget install --id Microsoft.DotNet.SDK.10 --exact
```

- Acesso ao NuGet na primeira compilação (pacotes `FirebirdSql.Data.FirebirdClient`, `PdfPig`, `Fody`, `Costura.Fody` e, nos testes, `xunit`).

## Gerar o .exe

```bash
powershell -ExecutionPolicy Bypass -File build.ps1
```

Compila a solução, roda os testes e gera `dist/NFCeHub.exe` com `LEIA-ME.txt` e `config.example.ini` junto. As bibliotecas (Firebird, PdfPig) ficam embutidas no `.exe` pelo Costura, então não há DLLs para distribuir. `-SkipTests` pula os testes.

A `dist/` não é versionada no Git. A cada envio para o branch `main`, o GitHub compila, testa e guarda a `dist/` como artefato da execução (aba *Actions*). Para publicar uma versão, anexe o `NFCeHub.exe` numa *Release* do GitHub.

## Testes

```bash
dotnet test NFCeHub.sln -c Release
```

Os testes cobrem faixas, leitura de listas (chaves de acesso, tabelas e texto livre), descompactação e identificação dos XMLs e o `config.ini`. Não precisam do banco e só usam dados inventados.

## Rodar pelo código-fonte

```bash
dotnet build NFCeHub.sln -c Release
```

O programa procura o `config.ini` na mesma pasta do `.exe` (`src/NFCeHub/bin/Release/net48/`). Na primeira vez abre a tela de configuração e grava o arquivo. Ele tem a senha do banco: fica fora do Git e não vai para o cliente.

## Linha de comando (testes)

O `nfcehub-cli.exe` procura o `config.ini` na pasta dele (`src/NFCeHub.Cli/bin/Release/net48/`); `--config` aponta outro arquivo.

```bash
src\NFCeHub.Cli\bin\Release\net48\nfcehub-cli.exe --series 1 --start 109370 --end 109375 --output C:\temp\xml
```

```bash
src\NFCeHub.Cli\bin\Release\net48\nfcehub-cli.exe --list relatorio.pdf --output C:\temp\xml --by-month
```

Opções: `--config`, `--series`, `--start`, `--end`, `--list` (PDF/TXT/CSV), `--output`, `--no-cancellation`, `--include-unauthorized`, `--by-month` e `--print-text` (mostra o texto extraído de um PDF, para ajustar a leitura de um formato novo).
