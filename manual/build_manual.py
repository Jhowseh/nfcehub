import json
import logging
import re
from datetime import date
from pathlib import Path

from fpdf import FPDF
from fpdf.enums import XPos, YPos
from fpdf.fonts import FontFace
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent
IMG = HERE / "img"
TMP = IMG / "generated"
README_IMG = ROOT / "docs" / "img"
OUTPUT = ROOT / "dist" / "Manual-NFCeHub.pdf"
FONTS = Path(r"C:\Windows\Fonts")

COMPANY = "PBtec - Soluções em Automação"
PHONE = "(83) 3224-3080"
DEVELOPERS = ["José Henrique", "Erick Henrique", "Eduardo Vasconcelos"]
VERSION = re.sub(r"^(\d+\.\d+)\.0$", r"\1", re.search(r"<Version>(.+?)</Version>",
                 (ROOT / "Directory.Build.props").read_text(encoding="utf-8"))[1])
MONTHS = ("janeiro", "fevereiro", "março", "abril", "maio", "junho", "julho", "agosto", "setembro", "outubro",
          "novembro", "dezembro")

TEXT, MUTED, FAINT = (31, 35, 40), (89, 99, 110), (101, 109, 118)
BORDER, CARD, LINK = (209, 217, 224), (246, 248, 250), (9, 105, 218)
DARK_BORDER = (48, 54, 61)
VIOLET, GREEN, YELLOW, RED = (139, 92, 246), (26, 127, 55), (154, 103, 0), (207, 34, 46)
DOT_GREEN = (63, 185, 80)
WARNING_BG, WARNING_BORDER = (255, 248, 197), (212, 167, 44)
TIP_BG, TIP_BORDER = (240, 236, 254), (196, 181, 253)


def prepare_images():
    TMP.mkdir(parents=True, exist_ok=True)
    markers = json.loads((IMG / "markers.json").read_text(encoding="utf-8"))
    annotate(IMG / "main.png", markers["main"], TMP / "main.png")
    annotate(IMG / "settings.png", markers["settings"], TMP / "settings.png")
    frame(IMG / "import.png", TMP / "import.png")
    frame(IMG / "export.png", TMP / "export.png")
    README_IMG.mkdir(parents=True, exist_ok=True)
    for name in ("main", "import", "export"):
        frame(IMG / f"{name}-dark.png", README_IMG / f"{name}.png", color=DARK_BORDER)


def frame(source, target, radius=26, color=BORDER):
    img = Image.open(source).convert("RGBA")
    ss = 3
    W, H = img.width * ss, img.height * ss
    mask = Image.new("L", (W, H), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, W - 1, H - 1], radius * ss, fill=255)
    mask = mask.resize(img.size, Image.LANCZOS)
    out = Image.new("RGBA", img.size, (255, 255, 255, 0))
    out.paste(img, (0, 0), mask)
    border = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(border).rounded_rectangle([1, 1, W - 2, H - 2], radius * ss, outline=color + (255,), width=3 * ss)
    out.alpha_composite(border.resize(img.size, Image.LANCZOS))
    out.save(target)
    return target


def annotate(source, markers, target):
    screen = Image.open(frame(source, target.with_suffix(".tmp.png")))
    ss = 3
    r = round(13 * markers["scale"])
    margin = 2 * r + 10
    img = Image.new("RGBA", (screen.width + 2 * margin, screen.height), (255, 255, 255, 0))
    img.alpha_composite(screen, (margin, 0))
    layer = Image.new("RGBA", (img.width * ss, img.height * ss), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    font = ImageFont.truetype(str(FONTS / "segoeuib.ttf"), round(r * ss * 1.15))
    for n, (x, y, side) in enumerate(markers["points"], start=1):
        x += margin + {"l": -(r + 7), "r": r + 7, "c": 0}[side]
        x, y, R = x * ss, y * ss, r * ss
        d.ellipse([x - R - 3 * ss, y - R - 3 * ss, x + R + 3 * ss, y + R + 3 * ss], fill=(255, 255, 255, 255))
        d.ellipse([x - R, y - R, x + R, y + R], fill=VIOLET + (255,))
        d.text((x, y + ss), str(n), font=font, fill=(255, 255, 255, 255), anchor="mm")
    img.alpha_composite(layer.resize(img.size, Image.LANCZOS))
    img.save(target)
    target.with_suffix(".tmp.png").unlink()


def aspect_ratio(path):
    with Image.open(path) as im:
        return im.height / im.width


def icon(name):
    return str(IMG / f"ic_{name}.png")


class Manual(FPDF):
    def __init__(self):
        super().__init__(format="A4", unit="mm")
        self.set_margins(18, 22, 18)
        self.set_auto_page_break(True, 22)
        for family, style, file in (("Segoe", "", "segoeui.ttf"), ("Segoe", "B", "segoeuib.ttf"),
                                    ("Segoe", "I", "segoeuii.ttf"), ("SegoeSB", "", "seguisb.ttf"),
                                    ("SegoeSB", "B", "segoeuib.ttf"),
                                    ("Mono", "", "consola.ttf")):
            self.add_font(family, style, str(FONTS / file))
        self.set_title("NFCeHub – Manual do usuário")
        self.set_author("PBtec")
        self.set_creator("PBtec")

    def header(self):
        if self.page_no() == 1:
            return
        self.set_xy(18, 10)
        self.set_font("Mono", size=7.5)
        self.set_char_spacing(1.6)
        self.set_text_color(*MUTED)
        self.cell(0, 4, "NFCEHUB · MANUAL DO USUÁRIO")
        self.set_char_spacing(0)
        self.image(str(IMG / "bar.png"), x=self.w - 18 - 16, y=11.2, w=16)
        self.set_y(22)

    def footer(self):
        if self.page_no() == 1:
            return
        self.set_draw_color(*BORDER)
        self.set_line_width(0.2)
        self.line(18, self.h - 16, self.w - 18, self.h - 16)
        self.image(str(IMG / "logo.png"), x=18, y=self.h - 13.5, h=8)
        self.set_xy(30, self.h - 11.5)
        self.set_font("Segoe", size=8)
        self.set_text_color(*FAINT)
        self.cell(80, 4, "PBtec · soluções em automação")
        self.set_xy(self.w - 18 - 60, self.h - 11.5)
        self.cell(60, 4, f"Página {self.page_no()}", align="R")

    @property
    def content_width(self):
        return self.w - self.l_margin - self.r_margin

    def space(self, mm):
        self.set_y(self.get_y() + mm)

    def ensure_space(self, mm):
        if self.get_y() + mm > self.page_break_trigger:
            self.add_page()

    def section(self, number, icon_name, title, subtitle=None):
        self.ensure_space(30)
        self.set_font("Mono", size=8)
        self.set_char_spacing(1.6)
        self.set_text_color(*MUTED)
        self.cell(0, 4, f"PASSO {number:02d}" if isinstance(number, int) else number,
                  new_x=XPos.LMARGIN, new_y=YPos.NEXT)
        self.set_char_spacing(0)
        self.space(1.5)
        y = self.get_y()
        self.image(icon(icon_name), x=self.l_margin, y=y + 0.8, w=6.5)
        self.set_xy(self.l_margin + 9, y)
        self.set_font("Segoe", "B", 18)
        self.set_text_color(*TEXT)
        self.cell(0, 8, title, new_x=XPos.LMARGIN, new_y=YPos.NEXT)
        if subtitle:
            self.set_font("Segoe", size=10.5)
            self.set_text_color(*MUTED)
            self.multi_cell(0, 5.4, subtitle, new_x=XPos.LMARGIN, new_y=YPos.NEXT)
        self.space(1.5)
        self.image(str(IMG / "bar.png"), x=self.l_margin, y=self.get_y(), w=14)
        self.space(6)

    def paragraph(self, text, color=TEXT, size=10.5, width=0, x=None):
        self.set_font("Segoe", size=size)
        self.set_text_color(*color)
        if x is not None:
            self.set_x(x)
        self.multi_cell(width, 5.6, text, markdown=True, new_x=XPos.LMARGIN, new_y=YPos.NEXT)
        self.space(2.5)

    subtitle_height = 8.5

    def subtitle(self, text):
        self.ensure_space(16)
        self.set_font("SegoeSB", size=12)
        self.set_text_color(*TEXT)
        self.cell(0, 7, text, new_x=XPos.LMARGIN, new_y=YPos.NEXT)
        self.space(self.subtitle_height - 7)

    def number_badge(self, x, y, number, d=6.2, color=VIOLET, size=9.5):
        self.set_fill_color(*color)
        self.ellipse(x, y, d, d, style="F")
        self.set_font("Segoe", "B", size)
        self.set_text_color(255, 255, 255)
        self.set_xy(x, y + 0.1)
        self.cell(d, d, str(number), align="C")

    def steps(self, items, x=None, width=None, d=6.2):
        x = self.l_margin if x is None else x
        width = width or (self.w - self.r_margin - x)
        for i, text in enumerate(items, start=1):
            self.ensure_space(12)
            y = self.get_y()
            self.number_badge(x, y, i, d)
            self.set_font("Segoe", size=10.5)
            self.set_text_color(*TEXT)
            self.set_xy(x + d + 3.5, y + 0.4)
            self.multi_cell(width - d - 3.5, 5.6, text, markdown=True, align="L",
                            new_x=XPos.LMARGIN, new_y=YPos.NEXT)
            self.set_y(max(self.get_y(), y + d) + 2.2)

    def bullets(self, items, x=None, width=None, size=10.5):
        x = self.l_margin if x is None else x
        width = width or (self.w - self.r_margin - x)
        for text in items:
            self.ensure_space(10)
            y = self.get_y()
            self.set_fill_color(*VIOLET)
            self.ellipse(x + 0.6, y + 2.1, 1.7, 1.7, style="F")
            self.set_font("Segoe", size=size)
            self.set_text_color(*TEXT)
            self.set_xy(x + 5, y)
            self.multi_cell(width - 5, 5.6, text, markdown=True, align="L", new_x=XPos.LMARGIN, new_y=YPos.NEXT)
            self.space(1.6)

    def bullets_height(self, items, x=None, width=None, size=10.5):
        x = self.l_margin if x is None else x
        width = width or (self.w - self.r_margin - x)
        self.set_font("Segoe", size=size)
        return sum(len(self.multi_cell(width - 5, 5.6, text, markdown=True, align="L", dry_run=True,
                                       output="LINES")) * 5.6 + 1.6 for text in items)

    def table_rows(self, widths, rows, bold_first_column=True, first_column_colors=None, padding=(1.8, 2.6)):
        self.set_font("Segoe", size=9.5)
        self.set_text_color(*TEXT)
        self.set_fill_color(255, 255, 255)
        self.set_draw_color(*BORDER)
        self.set_line_width(0.2)
        with self.table(col_widths=widths, line_height=5.6, padding=padding, markdown=True,
                        headings_style=FontFace(family="SegoeSB", color=LINK, fill_color=CARD, size_pt=9.5),
                        cell_fill_mode="NONE", text_align="LEFT") as t:
            for i, row in enumerate(rows):
                r = t.row()
                for j, value in enumerate(row):
                    style = None
                    if i and j == 0 and bold_first_column:
                        color = first_column_colors[i - 1] if first_column_colors else MUTED
                        style = FontFace(family="SegoeSB", color=color, size_pt=9.5)
                    r.cell(value, style=style)
        self.space(4)

    def callout(self, title, text, kind="tip"):
        background, border, title_color = ((WARNING_BG, WARNING_BORDER, YELLOW) if kind == "warning"
                                           else (TIP_BG, TIP_BORDER, (109, 40, 217)))
        inner_width = self.content_width - 10
        self.set_font("Segoe", size=10)
        lines = self.multi_cell(inner_width, 5.3, text, markdown=True, align="L", dry_run=True, output="LINES")
        height = 7 + 6 + len(lines) * 5.3 + 3
        self.ensure_space(height + 4)
        x, y = self.l_margin, self.get_y()
        self.set_fill_color(*background)
        self.set_draw_color(*border)
        self.set_line_width(0.3)
        self.rect(x, y, self.content_width, height, style="DF", round_corners=True, corner_radius=3)
        self.set_xy(x + 5, y + 4)
        self.set_font("SegoeSB", size=10.5)
        self.set_text_color(*title_color)
        self.cell(0, 6, title, new_x=XPos.LMARGIN, new_y=YPos.NEXT)
        self.set_x(x + 5)
        self.set_font("Segoe", size=10)
        self.set_text_color(*TEXT)
        self.multi_cell(inner_width, 5.3, text, markdown=True, align="L", new_x=XPos.LMARGIN, new_y=YPos.NEXT)
        self.set_y(y + height + 5)

    def figure(self, path, width, caption=None, x=None):
        height = width * aspect_ratio(path)
        self.ensure_space(height + (8 if caption else 2))
        x = self.l_margin + (self.content_width - width) / 2 if x is None else x
        y = self.get_y()
        self.image(str(path), x=x, y=y, w=width)
        self.set_y(y + height + 2)
        if caption:
            self.set_font("Segoe", "I", 8.5)
            self.set_text_color(*FAINT)
            self.cell(0, 4, caption, align="C", new_x=XPos.LMARGIN, new_y=YPos.NEXT)
        self.space(4)

    @staticmethod
    def tree_height(entries):
        return 8 + len(entries) * 8 + 4

    def tree(self, entries, name_width=82):
        height = self.tree_height(entries) - 4
        self.ensure_space(height + 4)
        x, y = self.l_margin, self.get_y()
        self.set_fill_color(*CARD)
        self.set_draw_color(*BORDER)
        self.set_line_width(0.3)
        self.rect(x, y, self.content_width, height, style="DF", round_corners=True, corner_radius=4)
        for i, (level, icon_name, name, note) in enumerate(entries):
            indent = level * 8
            yy = y + 5 + i * 8
            if level:
                self.set_draw_color(*BORDER)
                self.line(x + 0.5 + indent, yy - 4.5, x + 0.5 + indent, yy + 2.6)
                self.line(x + 0.5 + indent, yy + 2.6, x + 4 + indent, yy + 2.6)
            self.image(icon(icon_name), x=x + 6 + indent, y=yy, w=5)
            self.set_xy(x + 13 + indent, yy)
            self.set_font("Mono", size=9.5)
            self.set_text_color(*TEXT)
            self.cell(name_width - indent, 5, name)
            self.set_font("Segoe", size=9.5)
            self.set_text_color(*FAINT)
            self.cell(0, 5, note)
        self.set_y(y + height + 4)

    def feature_card(self, x, y, w, h, icon_name, title, description):
        self.set_fill_color(*CARD)
        self.set_draw_color(*BORDER)
        self.set_line_width(0.3)
        self.rect(x, y, w, h, style="DF", round_corners=True, corner_radius=4)
        self.image(icon(icon_name), x=x + 6, y=y + 6, w=7)
        self.set_xy(x + 6, y + 16)
        self.set_font("SegoeSB", size=11.5)
        self.set_text_color(*LINK)
        self.multi_cell(w - 12, 5.5, title, align="L")
        self.set_xy(x + 6, self.get_y() + 1.5)
        self.set_font("Segoe", size=9.5)
        self.set_text_color(*MUTED)
        self.multi_cell(w - 12, 5, description, align="L")


def cover(pdf):
    pdf.add_page()
    pdf.image(str(IMG / "logo.png"), x=18, y=22, h=30)
    y = 92
    pdf.set_xy(18, y)
    pdf.set_font("Mono", size=10)
    pdf.set_char_spacing(2.4)
    pdf.set_text_color(*MUTED)
    pdf.cell(0, 5, "MANUAL DO USUÁRIO")
    pdf.set_char_spacing(0)
    pdf.set_xy(16.5, y + 8)
    pdf.set_font("Segoe", "B", 46)
    pdf.set_text_color(*TEXT)
    pdf.cell(0, 20, "NFCeHub")
    pdf.set_xy(18, y + 31)
    pdf.set_font("Segoe", size=15)
    pdf.set_text_color(*MUTED)
    pdf.multi_cell(160, 7.5, "Os XMLs das NFC-e do SysPDV, prontos para a contabilidade.")
    pdf.image(str(IMG / "bar.png"), x=18, y=y + 51, w=22)
    pdf.set_fill_color(*DOT_GREEN)
    pdf.ellipse(45, y + 49.9, 2.6, 2.6, style="F")
    pdf.set_xy(50, y + 48.7)
    pdf.set_font("Segoe", size=10)
    pdf.set_text_color(*FAINT)
    pdf.cell(0, 5, "Instalação, configuração e uso")

    y = 172
    gap = 6
    w = (pdf.content_width - 2 * gap) / 3
    cards = [
        ("layers", "Várias faixas de uma vez",
         "Digite as faixas de qualquer série ou importe a relação de notas do contador."),
        ("folder", "Tudo organizado",
         "Uma pasta por série, por mês se quiser, e um relatório das notas que faltaram."),
        ("database", "Só leitura",
         "O NFCeHub só lê o banco do SysPDV. Nunca altera, transmite ou apaga nada."),
    ]
    for i, (icon_name, title, text) in enumerate(cards):
        pdf.feature_card(18 + i * (w + gap), y, w, 58, icon_name, title, text)

    pdf.set_xy(18, 262)
    pdf.set_font("Segoe", size=9)
    pdf.set_text_color(*FAINT)
    today = date.today()
    pdf.cell(0, 5, f"Versão {VERSION} · {MONTHS[today.month - 1]} de {today.year}")
    pdf.set_xy(18, 267)
    pdf.cell(0, 5, f"PBtec · soluções em automação · {PHONE}")


def page_intro(pdf):
    pdf.add_page()
    pdf.section("ANTES DE COMEÇAR", "xml", "O que é o NFCeHub")
    pdf.paragraph(
        "O NFCeHub gera os arquivos **XML das NFC-e** direto do banco de dados do SysPDV, para enviar ao "
        "contador ou guardar.")
    pdf.paragraph(
        "O SysPDV tem uma rotina própria para exportar os XMLs, mas ela é lenta e pouco prática quando é "
        "preciso gerar notas de várias faixas e séries diferentes. No NFCeHub tudo é feito em uma única tela:")
    pdf.bullets([
        "**Várias faixas de uma vez:** uma nota, um intervalo como \"122 a 150\" ou vários intervalos, de uma ou "
        "mais séries, no mesmo pedido.",
        "**Importação da relação de notas:** a lista pode vir de um arquivo PDF, Word, TXT ou CSV, sem digitar número "
        "por número.",
        "**Resultado organizado:** com mais de uma série, cada série vai para a sua pasta, e as notas que não "
        "puderam ser exportadas ficam listadas num relatório.",
        "**Mais rápido:** os XMLs são lidos direto do banco, sem passar pelas telas do SysPDV.",
    ])
    pdf.space(2)
    pdf.callout("O NFCeHub só lê o banco do SysPDV",
                "Ele nunca grava, altera, transmite ou apaga nada no SysPDV. Os XMLs são gravados exatamente como "
                "estão no banco, sem nenhuma alteração, então a **assinatura digital continua válida**.")

    pdf.subtitle("Em resumo")
    pdf.steps([
        "**Configure uma vez** o acesso ao banco do SysPDV (Passo 2).",
        "**Informe as notas:** digite a série e os números ou importe a relação enviada pelo contador.",
        "**Escolha a pasta** onde os XMLs serão gravados.",
        "Clique em **Gerar XMLs**. No fim, um resumo mostra o que foi exportado.",
    ])


def page_install(pdf):
    pdf.add_page()
    pdf.section(1, "folder", "Instalação",
                "O NFCeHub é um arquivo só, o NFCeHub.exe. Não tem instalador.")
    pdf.steps([
        "Crie uma pasta no computador **servidor do SysPDV**, por exemplo **C:\\NFCeHub**, e copie o "
        "**NFCeHub.exe** para ela.",
        "Para facilitar, crie um atalho na Área de Trabalho: clique com o botão direito no NFCeHub.exe → "
        "**Enviar para** → **Área de trabalho (criar atalho)**. No Windows 11, clique antes em "
        "**Mostrar mais opções**.",
        "Abra o programa. Na primeira vez aparece a tela de **Configuração** (próximo passo).",
    ])
    pdf.callout("O Windows pode pedir confirmação na primeira vez",
                "Se aparecer \"O Windows protegeu o computador\", clique em **Mais informações** → **Executar assim "
                "mesmo**. Se o antivírus bloquear, libere a pasta do programa (por exemplo, C:\\NFCeHub).")
    pdf.callout("Use uma pasta onde o usuário possa gravar",
                "A configuração é salva no arquivo **config.ini**, na mesma pasta do NFCeHub.exe. Não coloque o "
                "programa em C:\\Program Files: sem permissão de administrador, a configuração não é salva.",
                "warning")
    pdf.subtitle("Requisitos")
    pdf.bullets([
        "Windows 7 SP1 ao Windows 11.",
        "**.NET Framework 4.8**. No Windows 10 e 11 ele já vem instalado. Em Windows mais antigo, se o "
        "programa não abrir, instale o .NET Framework 4.8 pelo Windows Update.",
        "Acesso ao banco do SysPDV: no próprio servidor ou num computador da rede que enxergue o servidor.",
    ])
    pdf.space(2)
    pdf.subtitle("Como a pasta fica depois de usar")
    pdf.tree([
        (0, "folder", "C:\\NFCeHub", ""),
        (1, "xml", "NFCeHub.exe", "o programa"),
        (1, "sliders", "config.ini", "criado ao salvar a configuração"),
    ], name_width=62)


def page_config(pdf):
    pdf.add_page()
    pdf.section(2, "database", "Primeira configuração",
                "Na primeira vez, o programa pede os dados de acesso ao banco do SysPDV.")
    pdf.figure(TMP / "settings.png", 128, "Tela de configuração (os números indicam os itens explicados abaixo)")
    pdf.steps([
        "**Servidor, porta e arquivo do banco** já vêm preenchidos com o que o SysPDV deste computador usa. "
        "Normalmente não é preciso mudar.",
        "**Usuário**: normalmente **SYSDBA**.",
        "**Senha**: digite a senha do banco do SysPDV (**masterkey**).",
        "**Testar conexão**: confere se está tudo certo. Deve aparecer \"Conectado!\" em verde.",
        "**Salvar**: grava a configuração (a tecla **Enter** também salva). Pronto, não será pedida de novo.",
    ])
    pdf.callout("Para mudar depois",
                "Clique em **Configurações**, na tela principal. Para configurar outro computador que usa o mesmo "
                "servidor, você pode copiar o **config.ini** para a pasta do NFCeHub.exe dele.")


def page_main(pdf):
    pdf.add_page()
    pdf.section(3, "layers", "A tela principal", "Tudo é feito nesta tela.")
    pdf.figure(TMP / "main.png", 156, "Tela principal do NFCeHub (dados fictícios)")
    pdf.steps([
        "**Status da conexão**: bolinha verde = conectado ao SysPDV; vermelha = sem conexão. Passe o mouse "
        "sobre ela para ver os detalhes, como a última NFC-e emitida no banco.",
        "**Série, Nº inicial e Nº final**: as notas que você quer. Para uma nota só, deixe o nº final em branco.",
        "**+**: põe a faixa digitada na lista (ou tecle **Enter**).",
        "**Importar Documento**: lê a relação de notas de um PDF, Word, TXT ou CSV (Passo 5).",
        "**Lista de faixas**: o que será exportado, com a quantidade de notas. O **×** tira uma faixa; "
        "**Limpar lista** tira todas.",
        "**Onde salvar**: a pasta onde os XMLs serão gravados. O botão da pasta abre a janela para escolher.",
        "**Opções**: cancelamentos, pastas por mês e notas sem autorização (Passo 6).",
        "**Gerar XMLs**: começa a exportação.",
        "**Abrir pasta**: abre a pasta de destino no Explorador de Arquivos.",
        "**Configurações**: muda servidor, usuário ou senha do banco.",
        "**Sobre**: versão do programa e contato da PBtec.",
    ])


def page_export(pdf):
    pdf.add_page()
    pdf.section(4, "download", "Gerar os XMLs", "Do pedido do contador aos arquivos na pasta.")
    pdf.steps([
        "Em **Selecionar notas**, digite a série, o nº inicial e o nº final e clique em **+**. Repita para cada "
        "faixa, de qualquer série. Exemplo: série 1, de 122 a 150; depois série 2, de 160 a 170.",
        "Em **Onde salvar**, confira a pasta de destino.",
        "Clique em **Gerar XMLs**. A barra mostra o andamento. O botão **Parar** interrompe a exportação; os "
        "XMLs já gravados ficam na pasta.",
        "No fim aparece o resumo. Clique em **Abrir pasta** para ver os arquivos.",
    ])
    pdf.figure(TMP / "export.png", 108, "Resumo no fim da exportação (dados fictícios)")
    pdf.paragraph(
        "Cada quadro do resumo mostra quantas notas ficaram em cada situação: **autorizadas** (verde), "
        "**canceladas** (azul), **sem autorização** (amarelo), **não encontradas** e **com erro** (vermelho). "
        "O que significa cada uma está no Passo 6.")
    pdf.subtitle("Bom saber")
    pdf.bullets([
        "Se você digitou uma faixa e clicou direto em **Gerar XMLs**, sem clicar em +, ela entra na lista sozinha.",
        "Faixas repetidas ou que se encostam na mesma série são juntadas automaticamente.",
        "Cada faixa pode ter até 200.000 notas.",
        "A pasta, a série e as opções ficam guardadas para a próxima vez.",
        "Uma nota com problema não interrompe as outras: ela vai para o relatório e a exportação continua.",
    ])


def page_import(pdf):
    pdf.add_page()
    pdf.section(5, "document", "Importar a relação de notas",
                "Quando o contador manda a lista das notas que faltam, não precisa digitar uma por uma.")
    pdf.steps([
        "Clique em **Importar Documento** e escolha o arquivo (PDF, Word, TXT ou CSV).",
        "Confira o que o programa encontrou: total de NFC-e, notas por série, faixas e avisos.",
        "Clique em **Adicionar à lista**. Depois, é só clicar em **Gerar XMLs**.",
    ])
    pdf.figure(TMP / "import.png", 72, "Conferência antes de pôr as notas na lista (dados fictícios)")
    pdf.subtitle("O que o programa reconhece no arquivo")
    rows = [
        ("Formato", "Exemplo e observações"),
        ("Chaves de acesso", "Os 44 dígitos da chave, juntos ou separados em grupos. É o jeito **mais seguro**: "
                             "a série e o número saem da própria chave, que tem dígito verificador."),
        ("Tabela de notas", "Colunas **Inicial / Final / Série** (como o relatório \"Notas não lançadas de "
                            "Saídas\" do sistema do contador), **Série / Número**, ou a \"Análise de Sequência "
                            "de Notas\" da Alterdata (várias séries no mesmo arquivo)."),
        ("Faixas escritas", "\"122 a 150\", \"122 até 150\", \"122-150\", \"122 / 150\" ou \"Inicial 122 Final 150\"."),
        ("Números soltos", "Um número por linha ou separados por vírgula. Usa a série digitada na tela, ou a "
                           "série escrita no arquivo (\"Série 2\")."),
    ]
    pdf.table_rows((40, 134), rows)
    pdf.callout("PDF escaneado não funciona",
                "O programa lê o texto do arquivo. Um PDF que é foto ou digitalização não tem texto para ler. Peça "
                "ao contador o PDF original, o arquivo do Word ou um TXT ou CSV, ou digite as faixas na tela. Linhas de "
                "**NF-e (modelo 55)** são ignoradas: o programa exporta só NFC-e.", "warning")


def page_files(pdf):
    pdf.add_page()
    pdf.section(6, "folder", "Onde ficam os XMLs", "O que é gravado na pasta de destino.")
    pdf.subtitle("Situação de cada nota")
    rows = [
        ("Situação", "O que significa", "O que é gravado"),
        ("Autorizada", "NFC-e autorizada pela SEFAZ.", "<chave>-nfe.xml"),
        ("Cancelada", "Autorizada e depois cancelada.", "A nota e o evento de cancelamento"),
        ("Sem autorização", "Rejeitada ou não transmitida.", "Nada (ou subpasta **Sem autorizacao**)"),
        ("Não encontrada", "Número não existe no banco.", "Nada"),
        ("Com erro", "Não foi possível ler a nota.", "Nada"),
    ]
    pdf.table_rows((32, 64, 78), rows, first_column_colors=[GREEN, LINK, YELLOW, RED, RED])
    pdf.subtitle("Opções")
    rows = [
        ("Opção", "O que faz"),
        ("Incluir XML do cancelamento", "Grava o evento de cancelamento das canceladas. Vem marcada."),
        ("Separar em pastas por mês", "Uma subpasta por mês de emissão, como **2026-10**."),
        ("Incluir notas sem autorização", "Grava os XMLs sem protocolo na subpasta **Sem autorizacao**. Não "
                                          "valem para a contabilidade: use só para conferência."),
    ]
    pdf.table_rows((58, 116), rows)
    entries = [
        (0, "folder", "C:\\XML-Contabilidade\\Outubro-2026", ""),
        (1, "folder", "Série 1", ""),
        (2, "folder", "2026-10", ""),
        (3, "xml", "2526102723...-nfe.xml", "um arquivo por nota"),
        (1, "folder", "Série 2", "mesma organização"),
        (1, "document", "Relatorio-NFCe-nao-exportadas-...txt", "notas que não foram exportadas"),
    ]
    notes = [
        "Com uma série só, não é criada a pasta **Série**: os XMLs ficam direto na pasta escolhida.",
    ]
    pdf.ensure_space(pdf.subtitle_height + pdf.tree_height(entries) + pdf.bullets_height(notes))
    pdf.subtitle("Exemplo: duas séries, separando por mês")
    pdf.tree(entries, name_width=100)
    pdf.bullets(notes)


def page_contingency(pdf):
    pdf.add_page()
    pdf.section("BOM SABER", "alert", "Nota autorizada que aparece como \"sem autorização\"")
    pdf.paragraph(
        "Algumas NFC-e que estão autorizadas no SysPDV aparecem como **sem autorização**, com o detalhe "
        "**\"AUTORIZADO O USO DA NF-E\"**. Isso é um defeito do SysPDV, e não do NFCeHub.")
    pdf.subtitle("Por que acontece")
    pdf.steps([
        "O caixa envia a nota normalmente e a SEFAZ **autoriza**.",
        "A resposta não chega a tempo, o caixa entra em **contingência** e gera outra versão da mesma nota, "
        "com outra chave de acesso.",
        "Ao transmitir a contingência, o SysPDV junta a autorização da nota original à nota de contingência. "
        "O XML guardado fica **inválido**.",
    ])
    pdf.paragraph(
        "A nota autorizada de verdade não fica guardada no computador: nem no banco, nem na pasta "
        "**C:\\Syspdv\\NFCe\\Vendas**. Por isso o NFCeHub não gera o XML dessa nota (o contador recusaria o "
        "arquivo). Ela continua autorizada na SEFAZ e aparece no relatório de notas não exportadas.")


def page_faq(pdf):
    pdf.add_page()
    pdf.section("DÚVIDAS", "question", "Problemas comuns e soluções")
    rows = [
        ("Situação", "O que fazer"),
        ("Bolinha de status vermelha (\"Sem conexão com o banco do SysPDV\")",
         "Confira se o SysPDV abre normalmente. Em **Configurações**, confira o servidor e a senha e clique "
         "em **Testar conexão**."),
        ("\"Não conectou\" ao testar a conexão",
         "Servidor, porta, arquivo ou senha errados. Na rede, use o nome ou o IP do servidor do SysPDV e "
         "libere a porta 3050 no firewall."),
        ("Nota \"não encontrada\"",
         "Confira a série e o número. A nota pode ser de outra série ou o número pode ter sido inutilizado."),
        ("Nota \"sem autorização\"",
         "A nota foi rejeitada ou está em contingência sem transmitir. Transmita pelo SysPDV e gere de novo. "
         "Se o detalhe for \"AUTORIZADO O USO DA NF-E\", veja a página anterior."),
        ("\"Nenhuma NFC-e encontrada\" ao importar",
         "O arquivo não tem texto (PDF escaneado) ou está em outro formato. Peça o PDF original, o arquivo do "
         "Word, um TXT ou um CSV, ou digite as faixas."),
        ("\"Não foi possível gravar config.ini\"",
         "A pasta do programa não permite gravar (por exemplo, C:\\Program Files). Mova o NFCeHub.exe para "
         "uma pasta como C:\\NFCeHub."),
        ("Abro o programa e só aparece a janela que já estava aberta",
         "É normal: o NFCeHub abre uma janela só por vez. Procure-a na barra de tarefas."),
        ("O programa não abre no Windows 7 ou 8", "Instale o .NET Framework 4.8 pelo Windows Update."),
    ]
    pdf.table_rows((62, 112), rows, padding=(1.4, 2.6))
    pdf.callout("Precisa de ajuda?",
                f"Fale com a **{COMPANY}** pelo telefone **{PHONE}**. Se for um erro, informe o que apareceu "
                "na tela. O registro de erros do programa fica em **%LOCALAPPDATA%\\NFCeHub\\nfcehub.log**.")


def credits(pdf):
    pdf.space(2)
    height = max(32, 16 + 8 * len(DEVELOPERS))
    pdf.ensure_space(height + 12)
    pdf.subtitle("Créditos")
    x, y = pdf.l_margin, pdf.get_y()
    pdf.set_fill_color(*CARD)
    pdf.set_draw_color(*BORDER)
    pdf.set_line_width(0.3)
    pdf.rect(x, y, pdf.content_width, height, style="DF", round_corners=True, corner_radius=4)
    middle = x + pdf.content_width * 0.52

    def line(x0, icon_name, text, yy, family="Segoe", size=10.5):
        pdf.image(icon(icon_name), x=x0, y=yy + 0.2, w=5)
        pdf.set_xy(x0 + 8, yy)
        pdf.set_font(family, size=size)
        pdf.set_text_color(*TEXT)
        pdf.cell(80, 5.5, text)

    line(x + 7, "xml", f"NFCeHub · versão {VERSION}", y + 6, "SegoeSB", 11)
    line(x + 7, "layers", COMPANY, y + 14)
    line(x + 7, "phone", PHONE, y + 22)
    pdf.set_draw_color(*BORDER)
    pdf.line(middle - 4, y + 6, middle - 4, y + height - 6)
    pdf.set_xy(middle + 2, y + 6)
    pdf.set_font("Mono", size=8)
    pdf.set_char_spacing(1.6)
    pdf.set_text_color(*MUTED)
    pdf.cell(0, 5.5, "DESENVOLVIMENTO E ANÁLISE")
    pdf.set_char_spacing(0)
    for i, name in enumerate(DEVELOPERS):
        line(middle + 2, "user", name, y + 14 + 8 * i)
    pdf.set_y(y + height + 4)


def main():
    logging.getLogger("fontTools.subset").setLevel(logging.ERROR)
    prepare_images()
    pdf = Manual()
    cover(pdf)
    page_intro(pdf)
    page_install(pdf)
    page_config(pdf)
    page_main(pdf)
    page_export(pdf)
    page_import(pdf)
    page_files(pdf)
    page_contingency(pdf)
    page_faq(pdf)
    credits(pdf)
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    pdf.output(str(OUTPUT))
    print("Manual:", OUTPUT, f"({pdf.page_no()} pages)")


if __name__ == "__main__":
    main()
