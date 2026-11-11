# Funcionamento técnico

## Onde o SysPDV guarda os XMLs das NFC-e

Conferido no banco `syspdv_srv.fdb` (Firebird 2.5, porta 3050).

### TRANSACAO

Uma linha por transação do caixa. Chave primária: `TRNSEQ + TRNDAT + CXANUM`.

| Campo | Conteúdo |
|---|---|
| `TRNNFCESER` | Série da NFC-e, `CHAR(3)` (ex.: `'1  '`) |
| `TRNNFCENUM` | Número da NFC-e, `CHAR(10)` com zeros à esquerda (ex.: `'0000109375'`) |
| `TRNNFCECHVNFE` | Chave de acesso |
| `TRNTIP` | `'1'` venda · `'7'` venda que foi cancelada · `'0'` o cancelamento |
| `TRNSEQCAN` | No cancelamento, aponta para o `TRNSEQ` da venda |

A venda cancelada e o cancelamento têm o mesmo número de NFC-e. Transações sem `TRNNFCENUM` são NF-e modelo 55 (caixa 999) ou outras operações.

### TRANSACAO_XMLNOTA

O XML de cada transação, ligado à `TRANSACAO` por `TRNSEQ + CXANUM + TRNDAT` (índice `TRANSACAO_XMLNOTAINDEX1`).

| Campo | Conteúdo |
|---|---|
| `ID` | Chave primária |
| `TRNXML` | O XML compactado com zlib (BLOB) |
| `TRNSTA` | `AU` autorizada · `CA` cancelada · `RJ` rejeitada |
| `TRNXMLCSTAT` | cStat da SEFAZ (100 autorizada, 135 evento registrado, ...) |
| `TRNMENSNFE` | Mensagem da SEFAZ (motivo da rejeição, por exemplo) |

Conteúdo do `TRNXML` (depois de descompactar):

- Venda autorizada: `<nfeProc>` (NFC-e assinada + `<protNFe>` com o protocolo de autorização).
- Cancelamento: `<procEventoNFe>` (evento 110111).
- Rejeitada ou contingência ainda não transmitida: só `<NFe>`, sem protocolo.

O zlib é descompactado pulando os 2 bytes de cabeçalho (`0x78 ...`) e lendo o restante como deflate. Versões antigas podem ter gravado o XML sem compactar: se o conteúdo não começar com o cabeçalho zlib e começar com `<`, ele é usado como está.

A tabela `TRANSACAO_XMLLOG` guarda XMLs de log (envios com problema) e não é usada.

### Notas `AU` com o XML errado (defeito do SysPDV na contingência)

Algumas notas com `TRNSTA = 'AU'` e `TRNXMLCSTAT = 100` saem como **sem autorização**, com o detalhe "AUTORIZADO O USO DA NF-E". Não é erro de leitura do programa: o XML guardado pelo SysPDV não é o da nota autorizada.

O que acontece no caixa:

1. A venda é enviada em emissão normal (`tpEmis` 1) e a SEFAZ autoriza.
2. O caixa não recebe a resposta a tempo, entra em contingência offline e gera outra versão da mesma nota (`tpEmis` 9). Muda a chave de acesso (o dígito do tipo de emissão e o dígito verificador), mas o número e a série continuam os mesmos.
3. Ao transmitir a contingência, o SysPDV descobre que a nota já estava autorizada e junta o protocolo da emissão normal à NFC-e de contingência.

O `TRNXML` dessas notas fica assim:

- A raiz é `<procEventoNFe>` em vez de `<nfeProc>`, e a tag da nota vem quebrada: `<procEventoNFe><NFe>xmlns="..."`. O arquivo nem é um XML válido.
- A `<NFe>` é a de contingência (`tpEmis` 9, chave terminada em `9...`).
- O `<protNFe>` é da emissão normal: o `chNFe` do protocolo é diferente do `Id` da nota, e o `digVal` do protocolo não bate com o `DigestValue` da assinatura da nota.
- A chave autorizada é a do protocolo, a mesma que está em `TRANSACAO.TRNNFCECHVNFE`.

Exemplo conferido: série 2, nº 139873, de 17/10/2026. A chave autorizada é `25261027233500000120650020001398731021601788` (protocolo `225260421236435`), e a nota guardada tem a chave `25261027233500000120650020001398739021601783`.

A NFC-e autorizada (a versão de emissão normal, assinada) não fica guardada em lugar nenhum do computador:

- Não está na `TRANSACAO_XMLNOTA` nem na `TRANSACAO_XMLLOG`.
- A pasta `C:\Syspdv\NFCe\Vendas` só tem notas de contingência. Os arquivos são `<chave>-nfe.xml`: as que já foram transmitidas ficam como `<nfeProc>` com o protocolo da própria chave, e as outras ficam só como `<NFe>`, sem protocolo. Para essas notas com defeito, o arquivo de lá é idêntico, byte a byte, à `<NFe>` de contingência do banco, sem protocolo. Ler a pasta não resolve.
- Também não dá para reconstruir a versão autorizada a partir da de contingência. Trocar a chave, o `tpEmis` e o `cDV` e tirar `dhCont` e `xJust` não reproduz o `digVal` do protocolo, então a nota autorizada tem outras diferenças (o horário de emissão, por exemplo).

Por isso o programa não exporta esse XML como autorizado: ele é inválido e a contabilidade o recusaria. A nota aparece como sem autorização e entra no relatório de notas não exportadas. Na SEFAZ ela está autorizada com a chave do protocolo, e o defeito precisa ser tratado com o suporte do SysPDV.

Frequência medida no banco deste servidor, de 01/11/2025 a 11/11/2026:

| XML | Notas |
|---|---|
| Emissão normal autorizada, correto | 178.835 |
| Contingência autorizada com a própria chave (cStat 100 ou 150), correto | 2.347 |
| Contingência com o protocolo de outra chave (o defeito) | 907 `AU` e 1 `CA` |

## Exportação (`Export/Exporter.cs`)

Todo o SQL das tabelas do SysPDV fica em `Data/SysPdv.cs`: busca das notas, busca do XML, última NFC-e emitida e teste de conexão.

O banco não tem índice no número da NFC-e (`TRNNFCENUM`), nem em nenhuma outra tabela. Para não varrer a tabela uma vez por nota, a busca tem duas etapas:

1. Uma consulta leve, sem os XMLs, que acha as transações dos números pedidos (`TRNNFCENUM BETWEEN`, com o número completado com zeros até 10 dígitos). A série é conferida no programa, convertendo `TRNNFCESER` para número, porque pode estar gravada como `'1'` ou `'001'`.
2. Cada XML é buscado pela chave primária da `TRANSACAO_XMLNOTA` (`ID`), com uma consulta preparada uma vez só e reaproveitada.

### Busca por janelas de data (`SysPdv.FindRecords`)

Ler a `TRANSACAO` inteira custa caro com o cache do Firebird frio (o Firebird do SysPDV esvazia o cache poucos minutos depois de ficar sem uso). A busca usa o índice de data (`TRANSACAOINDEX2`, em `TRNDAT`) e lê só os dias necessários:

- Começa pelos últimos 7 dias e vai voltando no tempo, dobrando o tamanho da janela a cada passo (7, 14, 28 e depois 56 dias por vez). A primeira janela não tem limite superior de data, para pegar transações com o relógio do caixa adiantado.
- Em cada janela, além das transações dos números pedidos, vê o menor número de NFC-e vendido (`TRNTIP <> '0'`, sem os cancelamentos) em cada série. Como a numeração de uma série só cresce com o tempo, quando aparece uma venda com número menor que o menor número pedido daquela série, as notas pedidas já ficaram para trás e a série está concluída.
- Ao concluir todas as séries, lê mais 7 dias antes da última janela, para pegar a venda de uma nota cujo cancelamento caiu no dia seguinte.
- Se mesmo assim algum número pedido não for achado (nota inexistente, inutilizada ou numeração fora de ordem), esses números são procurados na tabela inteira, do jeito antigo. O resultado é sempre o mesmo da busca completa; só a nota não encontrada continua lenta com o cache frio.
- Se as janelas chegarem à data da primeira transação do banco, a tabela já foi lida toda e não há busca completa.

A ligação com a `TRANSACAO_XMLNOTA` é sempre um `LEFT JOIN` (as linhas sem XML são descartadas no programa). Com `JOIN` comum, o Firebird 2.5 escolhia o plano `JOIN (X NATURAL, T INDEX (RDB$PRIMARY394))`: lia a `TRANSACAO_XMLNOTA` inteira (a maior parte do banco, por causa dos XMLs) e buscava cada transação pela chave primária. O `LEFT JOIN` obriga a `TRANSACAO` a vir primeiro e o XML a ser buscado pelo índice `TRANSACAO_XMLNOTAINDEX1`.

Medido no banco de 3,2 GB (um ano de vendas, cerca de 500 transações por dia), para 1 nota de um mês atrás:

| Busca | Cache frio | Cache quente |
|---|---|---|
| `JOIN` antigo (lê a `TRANSACAO_XMLNOTA` inteira) | 55 a 121 s | 2,7 a 9,5 s |
| `LEFT JOIN` na tabela inteira | cerca de 12 s | 0,6 s |
| Janelas de data | lê só cerca de 2 meses da `TRANSACAO` | 0,65 s |

As faixas pedidas são ordenadas e juntadas antes (faixas da mesma série que se encostam ou se sobrepõem viram uma só).

Situação de cada nota:

| Situação | Quando | O que é gravado |
|---|---|---|
| autorizada | Há `<nfeProc>` com protocolo | `<chave>-nfe.xml` |
| cancelada | Há `<nfeProc>` e evento 110111 (ou `TRNSTA = 'CA'`) | `<chave>-nfe.xml` e, se a opção estiver ligada, `110111<chave>01-procEventoNFe.xml` |
| sem autorização | Só há XML sem protocolo, ou o XML com defeito da contingência (ver [Notas `AU` com o XML errado](#notas-au-com-o-xml-errado-defeito-do-syspdv-na-contingência)) | Nada, ou `Sem autorizacao/<chave>-nfe.xml` se a opção estiver ligada (o XML com defeito nunca é gravado) |
| não encontrada | Número não existe no banco | Nada |
| erro | Falha ao ler ou descompactar | Nada |

O nome do evento segue o padrão `<tpEvento><chave><nSeqEvento com 2 dígitos>-procEventoNFe.xml`. A chave é tirada do próprio XML (atributo `Id="NFe..."` ou `<chNFe>`), não da `TRANSACAO`. O XML é lido com a codificação Latin-1 só para essas buscas, o que mantém cada byte como está.

Quando as faixas pedidas têm mais de uma série, cada nota vai para a pasta `Série N` da sua série (a série da faixa pedida, ex.: `Série 1`, `Série 11`). Com uma série só, não é criada pasta de série. A pasta da série fica acima das outras: `Série N/AAAA-MM/Sem autorizacao/`. O relatório de notas não exportadas continua na pasta de destino.

Com a opção "Separar em pastas por mês", a pasta `AAAA-MM` vem das posições 3 a 6 da chave de acesso (AAMM da emissão), para a nota e o evento de cancelamento ficarem juntos.

O XML é gravado exatamente como está no banco, só descompactado: qualquer alteração invalidaria a assinatura digital. A gravação é feita num arquivo `.tmp` que depois substitui o definitivo com `MoveFileEx` (`MOVEFILE_REPLACE_EXISTING`), a mesma chamada que o Python usa. O `File.Move` do .NET Framework, com a checagem de arquivo existente antes, deixava a gravação de 4.000 arquivos três vezes mais lenta.

Quando alguma nota fica sem XML autorizado (não encontrada, sem autorização ou erro), é gravado na pasta de destino o relatório `Relatorio-NFCe-nao-exportadas-AAAAMMDD-HHMMSS.txt` (UTF-8 com BOM e quebras de linha do Windows, para abrir certo no Bloco de Notas e no Excel). Duas exportações no mesmo segundo usam o mesmo nome de relatório, e a segunda substitui a primeira.

Uma nota com problema não interrompe as outras. A exportação pode ser interrompida pelo botão Parar.

Desempenho medido neste servidor (5.001 números, 3.979 XMLs gravados): cerca de 20 s. Buscar os XMLs no banco leva menos de 2 s; o resto é gravação em disco (inclui a verificação do antivírus).

## Leitura da relação de notas (`Import/ListReader.cs`)

O texto do PDF é extraído (em `Import/DocumentText.cs`) com a biblioteca PdfPig. As palavras de cada página são agrupadas em linhas pela altura da base do texto (palavras com bases a até metade da altura da letra, no mínimo 2 pontos, ficam na mesma linha), de cima para baixo e da esquerda para a direita. TXT e CSV são lidos como UTF-8 e, se falhar, como Windows-1252. PDF escaneado (imagem) não tem texto para ler.

Para ver o texto que o programa extraiu de um PDF novo, use `nfcehub-cli.exe --print-text arquivo.pdf`.

Formatos reconhecidos, nesta ordem de preferência:

1. **Chaves de acesso** (44 dígitos, juntos ou em grupos de 4 separados por espaço, ponto ou hífen). Série e número saem da própria chave, e o dígito verificador (módulo 11) é conferido. Só vale modelo 65; chaves modelo 55 são contadas e ignoradas. Se o arquivo tem chaves válidas, só elas são usadas.
2. **Tabela com cabeçalho.** As colunas são reconhecidas pelo nome, na ordem em que aparecem no cabeçalho: inicial (`inicial`, `inicio`, `ini`), final (`final`, `fim`), série (`serie`, `ser`), número (`numero`, `num`, `nnf`, `documento`, `doc`) e modelo (`modelo`, `mod`). Precisa ter inicial e final, ou número. Com "Número inicial / Número final", a coluna "número" é descartada. Cada linha da tabela usa os primeiros números da linha, na ordem das colunas.
   - Só valem as linhas que vêm depois de um cabeçalho (CNPJ, inscrição estadual e nome da empresa no topo da página são ignorados).
   - Linhas com poucos números (número de página, por exemplo) são ignoradas.
   - Modelo 55 é ignorado; modelo diferente de 65 também.
   - Exemplo real: relatório "Notas não lançadas de Saídas" do sistema da Procontal, com as colunas `Inicial a Final  Série  Espécie`.
3. **Sem cabeçalho:** faixas escritas (`122 a 150`, `122 até 150`, `122-150`, `inicial 122 final 150`) e números soltos. O resto de uma linha de faixa (por exemplo "caixa 1") não vira nota.

Regras comuns:

- O texto é comparado sem acentos e em minúsculas ("nº" vira "no").
- Linhas com "NF-e", "NFe" ou "modelo 55" são ignoradas, a não ser que também tenham "NFC-e", "modelo 65" ou "cupom".
- "Série 2" numa linha vale para as linhas seguintes. Sem série no arquivo, vale a série digitada na tela.
- São removidos antes da leitura: CNPJ, CPF, datas (`25/09/2026`, `2026-09-25`), horas, valores com 2 decimais (`1.234,56`), percentuais, "página 1 de 3", "1/3" e números com 20 dígitos ou mais.
- Número com separador de milhar sem decimais (`264.371`) vira `264371`.
- Linhas de total, quantidade, CNPJ, CPF, inscrição, telefone e CEP são ignoradas.
- Um número inteiro não pode ser pedaço de um número maior nem de um decimal (`1.5`, `2,30`), mas pode vir seguido de vírgula numa lista (`200, 205, 206`).
- Faixas com 100.000 notas ou mais são descartadas (provável erro de leitura) e geram aviso. A linha inteira é descartada: os números das pontas não viram notas soltas.
- Limitação conhecida: o relatório `Relatorio-NFCe-nao-exportadas-....txt` do próprio programa não é lido de volta, porque a coluna de detalhe costuma trazer "NF-E" (por exemplo "AUTORIZADO O USO DA NF-E") e a linha é tratada como NF-e modelo 55.

Antes de pôr as notas na lista, a janela mostra o que foi encontrado (total por série e as primeiras faixas) para o cliente confirmar.

## Conexão com o banco (`Data/Database.cs`, `Config/AppSettings.cs`)

O `config.ini` é lido e gravado pelo `AppSettings`, que expõe cada chave como uma propriedade. As janelas e a linha de comando não usam os nomes das seções e chaves diretamente.


- Servidor, porta e arquivo do banco vêm do registro do SysPDV (`HKLM\SOFTWARE\WOW6432Node\Syspdv_server\UniConnection`, valores `Server`, `Database`, `Port`). O programa lê a visão de 32 bits do registro, que é onde o SysPDV (32 bits) grava, então funciona igual em processo de 32 ou 64 bits. Sem o registro, vale `127.0.0.1`, `3050` e `C:\Syspdv\syspdv_srv.fdb`.
- A conexão usa a biblioteca `FirebirdSql.Data.FirebirdClient`, que conversa direto com o servidor pela rede, sem `fbclient.dll`. Ela funciona com o Firebird 2.5 do SysPDV (testado).
- Toda consulta roda numa transação `READ COMMITTED` somente leitura, desfeita (rollback) no final. O próprio Firebird recusa qualquer gravação nela.
- Charset da conexão: `ISO8859_1` (o mesmo das tabelas do SysPDV). Pool de conexões desligado e tempo limite de conexão de 15 s.
- O `config.ini` é lido aceitando BOM, para funcionar se for salvo pelo Bloco de Notas. Linhas que começam com `;` ou `#` são ignoradas. As chaves são gravadas em minúsculas, como fazia a versão Python, então os dois leem o mesmo arquivo.

### config.ini

| Seção | Chave | Uso |
|---|---|---|
| `database` | `host`, `port`, `path`, `user`, `password` | Conexão com o Firebird |
| `output` | `xml_folder` | Última pasta escolhida |
| `options` | `series` | Última série usada |
| `options` | `include_cancellation`, `by_month`, `include_unauthorized` | Opções da tela (`yes`/`no`) |

A chave `fbclient` da versão Python não é mais usada e é retirada quando o arquivo é salvo.

## Janela (`MainWindow`, `SettingsWindow`, `Header`)

- A janela tem duas colunas para caber em telas de 768 px de altura: à esquerda as notas (campos, importar PDF e lista de faixas), à direita a pasta, as opções e o progresso.
- Ao abrir, busca a última NFC-e emitida nos últimos 120 dias (usa o índice de `TRNDAT`) e mostra só no texto que aparece ao passar o mouse na bolinha de status do cabeçalho.
- Acesso ao banco e leitura de PDF rodam em segundo plano (`Task.Run`), para a janela não travar. O progresso da exportação é lido pela janela a cada 80 ms.
- Enter num dos campos de número põe a faixa na lista. Os botões só com ícone (`+`, pasta, `×`) têm nome de acessibilidade, para leitores de tela.
- A pasta é escolhida com a janela moderna do Windows (`IFileOpenDialog` com `FOS_PICKFOLDERS`), em vez da janela em árvore antiga do .NET Framework.
- Os textos e as cores de cada situação (autorizada, cancelada, sem autorização, não encontrada, com erro) ficam numa tabela só, em `StatusLabels.cs`, usada pela mensagem de fim da exportação na janela e pelos destaques da caixa de resultado.
- Só uma instância do programa por sessão do Windows (mutex `Local\NFCeHub`). Ao abrir de novo, a janela já aberta vem para a frente: ela é achada pelo título `NFCeHub` (`App.Title`), que tem de ser igual ao `Title` da `MainWindow.xaml`.
- Log de erros em `%LOCALAPPDATA%\NFCeHub\nfcehub.log` (até 1 MB, 2 cópias antigas). Erros não tratados na janela vão para o log e aparecem numa mensagem, sem fechar o programa.
- Caixas de mensagem: o programa não usa a caixa padrão do Windows. `MessageDialog` é uma janela própria no tema (claro/escuro), com um selo redondo colorido conforme o tipo (pergunta e informação em azul, sucesso em verde, aviso em amarelo, erro em vermelho), título, subtítulo, uma faixa em degradê e os botões em pílula. Enter aciona o botão principal e Esc fecha (equivale a cancelar). Avisos tocam o som de exclamação do Windows e erros, o som de erro. `Dialogs.cs` monta o conteúdo de cada caso:
  - Importar PDF: destaques com o total de NFC-e, de faixas e de séries; tabela por série (intervalo de números e quantidade); as 12 primeiras faixas em etiquetas (com a série na frente quando há mais de uma); avisos de NF-e ignoradas e de leitura. Botões "Cancelar" e "Adicionar à lista".
  - Arquivo sem NFC-e: explica os formatos aceitos e mostra os avisos da leitura.
  - Fim da exportação: um destaque colorido por situação que apareceu (autorizadas em verde, canceladas em azul, sem autorização em amarelo, não encontradas e com erro em vermelho), a pasta de destino e o nome do relatório, quando houver. O selo é verde se tudo foi exportado e amarelo se houve problema ou interrupção. Botões "Fechar" e "Abrir pasta".
  - Erro inesperado: mensagem de erro no mesmo visual. Se a própria caixa falhar, cai na caixa padrão do Windows.
- Tela de configuração: janela independente (com botão próprio na barra de tarefas), sem bloquear a principal. Só uma aberta por vez. "Testar conexão" só abre a conexão e faz `SELECT 1 FROM RDB$DATABASE`, mostrando "Conectado!". Não conta os registros da `TRANSACAO_XMLNOTA`: o Firebird não guarda o total de linhas, então o `COUNT(*)` lê a tabela inteira e, num banco de 3 GB, levava cerca de 3 minutos. Os campos vêm do `config.ini`, completados com os valores do registro do SysPDV. Enter salva e Esc fecha.
- Tela Sobre (botão "Sobre" da tela principal): aparece na própria janela, como na versão de janela única do PreDANFE. O cabeçalho e os botões de baixo ficam parados; só o meio da tela (`Stage`) troca de conteúdo. As duas partes deslizam juntas em 240 ms, desacelerando no fim: ao abrir, a tela principal sai pela esquerda e o Sobre entra pela direita; ao voltar, o contrário. O `Stage` corta o que passa das bordas. Com as animações do Windows desligadas (`SystemParameters.ClientAreaAnimation`), a troca é imediata. Enquanto o Sobre está aberto, a tela principal fica oculta mas guarda o espaço, então a janela não muda de tamanho. O painel (`Controls/AboutPanel`) tem o título com o botão "Voltar", a versão em letras espaçadas e dois cartões alinhados com as colunas da tela principal (440 e 340 px): à esquerda a empresa, o telefone e o que o programa faz; à direita quem desenvolveu. A versão vem do assembly (`<Version>` do `Directory.Build.props`), sem o último número quando ele é zero (1.0.0 aparece como 1.0, como no manual). O botão "Sobre" abre e fecha o painel; "Voltar" e Esc também fecham.

### Tema visual (`Theme.cs`, `Themes/`)

- Paleta Primer (GitHub) clara e escura, seguindo o tema do Windows (`AppsUseLightTheme`). Barra em degradê ciano → violeta, ícones de traço violeta, cartões com cantos de 12 px e botões em pílula, iguais aos da versão Python e do PreDANFE.
- As cores viram recursos do WPF (`BgBrush`, `CardBrush`, `LineBrush`, `TextBrush`, `MutedBrush`, `FaintBrush`, `LinkBrush`, `HoverBrush`, `FieldBrush`, `GreenBrush`, `RedBrush`, `YellowBrush`, `VioletBrush`, `GradientBrush` e variações), definidos ao abrir conforme o tema do Windows.
- Os ícones são caminhos vetoriais na grade 24×24 (`Themes/Icons.xaml`), com traço 2 e pontas redondas, desenhados por `IconView`. Ficam nítidos em qualquer zoom.
- Tamanhos de fonte em pixels do WPF, convertidos dos pontos da versão Python (pontos × 96 / 72): título 22 pt, subtítulo 11 pt, seção 12 pt, corpo 10 pt, pequeno 9 pt.
- Logo: no tema claro usa `pbtec_logo_light.png`, cortado na área visível; no escuro, `pbtec_logo.png` com o fundo preto transformado em transparência pelo brilho de cada ponto (o vermelho do "PB" é mantido, o branco vira a cor do texto).
- Barra de título escura no Windows 10/11 via `DwmSetWindowAttribute` (atributos 20 e 19).
- O ponto de status do cabeçalho pulsa: anel que cresce de 4 a 10 px e some em 2 s, desacelerando no fim. A frase do status aparece numa dica ao passar o mouse sobre o ponto.
- Botão primário: degradê com texto branco; ao passar o mouse, degradê 15% mais claro; desativado, degradê 55% misturado ao fundo.
- Campo de texto: borda azul de 2 px com foco; o texto de dica some ao focar.
- O WPF já cuida do DPI (zoom de 125%, 150%) e do ícone na barra de tarefas. O programa ainda define um AppUserModelID próprio (`PBtec.NFCeHub`) para o Windows agrupar as janelas dele.

## Distribuição

- Alvo: .NET Framework 4.8, `AnyCPU`, sem preferência por 32 bits. Já vem no Windows 10 (desde a versão 1903) e no 11; no Windows 7 SP1, 8.1 e 10 antigos pode ser instalado pelo Windows Update.
- O Costura.Fody embute as DLLs (FirebirdClient, PdfPig e dependências) no `.exe`. Testado rodando só com o `.exe` e o `config.ini` numa pasta vazia.
- Código C# 12 compilado para .NET Framework 4.8, evitando recursos que precisam de tipos novos do .NET (registros, `init`, índices `^`).
- Versão, alvo do .NET e versão do C# ficam no `Directory.Build.props`, valendo para todos os projetos.
- O `build.ps1` compila, roda os testes (`tests/NFCeHub.Core.Tests`) e só então monta a `dist/`. O mesmo script roda no GitHub Actions (`.github/workflows/build.yml`) a cada envio para o `main`.
