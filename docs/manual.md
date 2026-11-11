# Manual em PDF

O manual do cliente (`dist/Manual-NFCeHub.pdf`) é gerado em duas etapas pelo `manual/build-manual.ps1`. O layout segue o do manual do PreDANFE: capa com o logo, cabeçalho e rodapé em cada página, passos numerados, caixas de dica e de aviso, tabelas e capturas de tela com marcadores numerados.

## Imagens das telas (`manual/Screens`)

O `nfcehub-screens.exe` é um programa de linha de comando que referencia o projeto `NFCeHub` e desenha as janelas reais do programa em PNG, sem abrir o banco do SysPDV. Ele grava em `manual/img/`, que fica fora do Git (as imagens são refeitas a cada geração do manual):

| Arquivo | Conteúdo |
|---|---|
| `main.png` | Janela principal com três faixas na lista e uma faixa digitada |
| `settings.png` | Tela de configuração com "Conectado!" |
| `import.png` | Caixa de conferência da importação (5 faixas, 2 séries, 2 linhas de NF-e ignoradas) |
| `export.png` | Resumo do fim da exportação (autorizadas, canceladas, sem autorização e não encontradas) |
| `markers.json` | Posição dos marcadores numerados de `main.png` e `settings.png` |
| `ic_*.png`, `logo.png`, `bar.png`, `app_icon.png` | Ícones de `Themes/Icons.xaml` em violeta, logo claro, barra em degradê e ícone do programa |

Como funciona:

- Os recursos do programa (logo, ícone) estão dentro do `NFCeHub.exe`. O WPF só deixa trocar o `Application.ResourceAssembly` quando não há assembly de entrada, então o campo `_resourceAssembly` do `Application` e o `BaseUriHelper.ResourceAssembly` são definidos por reflexão.
- É usado um `Application` comum, não o `App` do programa: o construtor do `Application` agenda o `OnStartup`, e o do `App` abriria a janela de verdade, tentaria conectar ao banco e aplicaria o tema do Windows. Os dicionários `Themes/Icons.xaml` e `Themes/Controls.xaml` são carregados à mão.
- O tema é sempre o claro, mesmo com o Windows no escuro: depois do `Theme.Apply`, o `Theme.IsDark` é desligado por reflexão e as cores são refeitas com a paleta clara.
- A configuração vem de um `config.ini` temporário com dados fictícios (servidor 192.168.0.10, pasta `C:\XML-Contabilidade\Outubro-2026`), apagado no fim.
- As janelas principal e de configuração não são mostradas. O conteúdo é tirado da janela, posto num `Border` com o fundo do tema, medido e desenhado com `RenderTargetBitmap` em 2× (192 dpi). As faixas entram na lista pelo método `AddRange` da janela, chamado por reflexão, e o ponto de status é ligado em verde.
- As duas caixas de mensagem são abertas pelas próprias funções de `Dialogs` (`ConfirmImport` e `ExportFinished`) com dados fictícios. Um tratador do evento `Loaded` da `MessageDialog` desenha o conteúdo e fecha a caixa, então elas aparecem na tela por um instante. A caixa de resumo é de aviso (tem notas com problema) e toca o som de exclamação do Windows.
- O texto é desenhado no modo `Ideal` do WPF. O programa usa o modo `Display` em alguns textos, que fica serrilhado quando ampliado.
- A posição de cada marcador é o ponto do controle convertido para a imagem, multiplicado pela escala. O lado (`l`, `r`) indica se o marcador fica à esquerda ou à direita do ponto.

## PDF (`manual/build_manual.py`)

Usa `fpdf2` e `Pillow`, com as fontes Segoe UI e Consolas do Windows.

- Recorta as capturas com cantos arredondados e borda e desenha os marcadores numerados (círculos violeta), numa margem acrescentada dos dois lados da imagem. As imagens prontas ficam em `manual/img/generated/`.
- A versão vem da tag `<Version>` do `Directory.Build.props`, sem o último número quando ele é zero (`1.0.0` aparece como "1.0"). O mês da capa é o da data de geração.
- O texto do manual está nas funções `page_*` do script. Empresa, telefone e créditos ficam nas constantes do início.
- As seções começam numa página nova, menos "Bom saber" (nota autorizada que aparece como "sem autorização"), que continua na página do Passo 6. Ao mudar o texto, confira se nenhuma página ficou só com um pedaço da anterior.
