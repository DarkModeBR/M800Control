# Protocolo HID do mouse M800 PRO (PAW3370)

Engenharia reversa do driver oficial `M800DC 3Mode DRIVER 2.0.exe` (instalador Inno Setup 5.1.7 →
`app/M800DC DRIVER.exe`, MFC 32 bits). Feita com desmontagem (Capstone), descompilação (Ghidra 11.2)
e emulação (Unicorn) do executável, e confirmada com o mouse real onde indicado.

Legenda de confiança: **[testado]** confirmado no mouse · **[código]** lido no código do driver,
ainda não enviado ao mouse · **[inferido]** dedução, não confirmada.

## 1. Identificação do dispositivo

| Campo | Valor |
|---|---|
| Vendor ID | `0x248A` |
| Product ID | `0x5B2E` = cabo USB · `0x5B2F` = receptor 2.4 GHz |
| Nome USB | "USB Gaming Mouse" / "XCTECH" |
| Modelo interno | `M802` (retornado pelo comando INFO) **[testado]** |

Coleções HID do mouse no cabo **[testado]**:

| Interface | Usage page / usage | Input | Feature | Uso |
|---|---|---|---|---|
| 0 | 0x0001 / 0x02 | 8 | – | movimento do mouse |
| 1 | 0x0001 / 0x06 | 8 | – | teclado (macros/atalhos) |
| 1 | 0x000C / 0x01 | 3 | – | teclas multimídia |
| 1 | 0x0001 / 0x00 | **33** | – | **notificações** do mouse para o PC |
| 1 | **0xFF00** / 0x01 | – | **33** | **configuração** (feature reports) |
| 2 | 0xFFEF / 0x00 | 64 | 64 | não usada pelo driver (provavelmente atualização de firmware) |

O driver procura `VID 248A`, PID `5B2E`/`5B2F` e escolhe a coleção pelo tamanho: feature report de
33 bytes = configuração, input report de 33 bytes = notificações. Ele prioriza o cabo.

## 2. Formato do pacote

Toda configuração é feita com **`HidD_SetFeature`** (33 bytes) na coleção 0xFF00. A única leitura
é o comando INFO, que usa `HidD_SetFeature` seguido de `HidD_GetFeature` com report ID `0x0C`.

```
byte  0     0x0C   report ID
byte  1     0x01   fixo
byte  2     CMD    comando
byte  3     0x00   fixo
byte  4     SEQ    contador incrementado a cada pacote (o mouse não parece validar)
byte  5     CHUNK  (índice_do_bloco << 4) | total_de_blocos  → 0x01 em comandos de um pacote só
byte  6     LEN    quantidade de bytes úteis a partir do byte 7
byte  7..32 DADOS  (até 26 bytes), resto preenchido com 0x00
```

O driver espera **60 ms** entre pacotes (`Sleep(0x3C)`).

Não existe comando para **ler** a configuração atual: o driver guarda tudo nos arquivos
`config/*.psf` e só envia o que mudou. A sua aplicação também precisa guardar o estado.

## 3. Comandos

| CMD | Nome | LEN | Dados (a partir do byte 7) | |
|---|---|---|---|---|
| `0x04` | Botão | 4 | `[índice, tipo, b1, b2]` (seção 6) | **[testado]** |
| `0x05` | Níveis de DPI | 18 | seção 4 (o mouse usa no máximo 6 níveis) | **[testado]** |
| `0x06` | Cores do DPI | 24 | 8 × `[R, G, B]`, uma por nível | **[testado]** |
| `0x07` | Polling rate | 1 | `0x01`=1000 Hz · `0x02`=500 · `0x04`=250 · `0x08`=125 | **[testado]** |
| `0x08` | Iluminação RGB | var. | seção 5 | **[testado]** (modo 0) |
| `0x09` | LOD | 1 | `1` = 1 mm · `2` = 2 mm (padrão 2) | [código] |
| `0x0A` | Debounce | 1 | tempo em ms, par, 0–30 (slider de 16 posições × 2 ms; padrão 12) | [código] |
| `0x0B` | Energia | 2 | `[minutos_para_dormir, apagar_LED_ao_mover]` (driver: 3–10 min, padrão 6; flag 0/1) | [código] |
| `0x0C` | Acordar ao mover | 1 | `0`/`1` (padrão 1) | [código] |
| `0x0E` | Conteúdo de macro | var. | seção 7 (vários blocos) | **[testado]** |
| `0x0F` | Reset de fábrica | 1 | `0xFF` | [código] |
| `0x20` | INFO (leitura) | 0 | bytes 5 e 6 = `0x00` | **[testado]** |

### 3.1 Resposta do INFO **[testado]**

Envie o pedido, espere ~10 ms e leia o feature report `0x0C`. A primeira leitura costuma vir vazia
(byte 6 = 0); o driver repete o pedido. Exemplo real:

```
0c 01 20 00 01 01 10 00 4d 38 30 32 25 01 00 70 15 ff 64 01 ff 01 00 00 00 00 00 00 ff 53 15 87 06
```

| Byte | Valor | Significado |
|---|---|---|
| 8–11 | `M802` | modelo (o driver procura exatamente "M802") |
| 15 | `0x70` | sensor: `25`=PAW3325, `27`=PAW3327, `35`=PAW3335, `60`=PMW3360, `70`=PAW3370, `89`=PMW3389 |
| 18 | `0x64` | bateria em % |
| 19 | `0x01` | `1` = carregando, `2` = carga completa (driver mostra 100%), outro = descarregando |
| 21 | `0x01` | mouse online (no receptor: 0 = mouse desligado/dormindo) |
| 12–14, 16–17, 20, 28–32 | | não usados pelo driver (12–13 talvez sejam versão de firmware) |

### 3.2 Notificações (mouse → PC) **[testado]**

Na coleção 0x0001/0x00 (input de 33 bytes) o mouse envia, quando você troca DPI ou polling pelo
próprio mouse:

```
byte 0 = 0x05, byte 1 = 0x06, byte 3 = código do polling (1/2/4/8), byte 4 = nível de DPI (1-based)
```

## 4. DPI (`0x05`)

```
dado 0      ((nível_atual + 1) << 4) | quantidade_de_níveis_habilitados
dado 1      máscara de níveis habilitados (bit i = nível i)
dado 2..17  8 níveis × 2 bytes (little-endian) com o valor de registrador
```

Valor de registrador = índice do DPI na tabela do sensor + deslocamento. Para o **PAW3370**
(deslocamento 0, 290 valores) **[código, tabela obtida por emulação]**:

- índices 0–199 → 50 a 10 000 DPI, passo 50 (`reg = DPI/50 − 1`)
- índices 200–289 → 10 100 a 19 000 DPI, passo 100 (`reg = 199 + (DPI − 10 000)/100`)

Tabelas dos outros sensores: `app/M800.Core/Protocol/DpiTables.g.cs`.

> **[testado]** O mouse usa no máximo **6 níveis**: com a máscara `0xFF` ele cicla 1→6→1 e ignora
> os níveis 7 e 8. O driver oficial só preenche 5, embora a máscara padrão (`0x3F`) habilite 6.

## 5. Iluminação (`0x08`)

```
dado 0   modo
dado 1   (brilho << 4) | velocidade      brilho 1–4 · velocidade 1–3 (posições dos sliders + 1)
dado 2.. depende do modo
```

| Modo | Nome no driver | Dados extras | LEN |
|---|---|---|---|
| 0 | None (desligado) | – | 2 |
| 1 | Colorful Streaming | `[direção 0/1]` | 3 |
| 2 | Breathing | `[n, R,G,B × n]` (1–7 cores, pretas são puladas) | 3·(n+1) |
| 3 | Steady | `[R, G, B]` | 5 |
| 4 | Neon | – | 2 |
| 5 | Snake | – | 2 |
| 6 | Colorful Steady | 7 × `[R, G, B]` | 23 |
| 7 | Flicker | 2 × `[R, G, B]` + zeros até 21 bytes; o driver zera o 1º byte (R da 1ª cor) | 23 |
| 8 | Stars Twinkle | – | 2 |
| 9 | Wave | – | 2 |

Cores sempre na ordem R, G, B.

## 6. Botões (`0x04`)

Índices no firmware: `0` esquerdo · `1` direito · `2` meio · `3` voltar · `4` avançar · `5` DPI.
(No arquivo `.psf` os botões 3/4 estão invertidos — "Key3" é avançar — e o driver os troca ao enviar.)

Cada ação tem 3 bytes `[tipo, b1, b2]`, tirados das tabelas do driver (`0x61E3F7`–`0x61E497`):

| Tipo | Ação | b1 | b2 |
|---|---|---|---|
| `0x00` | Desativado | 0 | 0 |
| `0x10` | Botão do mouse | máscara: `01` esq · `02` dir · `04` meio · `08` voltar · `10` avançar | 0 |
| `0x30` | Tecla de tiro | intervalo (5–255 ms) | repetições (0 = enquanto segurar). "Clique triplo" = `30 32 03` |
| `0x40` | DPI | `01` ciclo · `02` DPI+ · `03` DPI− | 0 |
| `0x50` | DPI Lock | valor (submenu de 10 opções; semântica não confirmada) | 0 |
| `0x60` | Trocar efeito de luz **[testado]** | `04` | 0 |
| `0x70` | Tecla / atalho | máscara de modificadores | uso HID da tecla |
| `0x80` | Multimídia **[testado]** | uso Consumer, byte baixo | uso Consumer, byte alto |
| `0x90` | Macro **[testado]** | `(modo_repetição << 4) \| slot` (slot 1–15) | repetições (só no modo 2) |

Modificadores (padrão HID): `01` Ctrl E · `02` Shift E · `04` Alt E · `08` Win E · `10` Ctrl D ·
`20` Shift D · `40` Alt D · `80` Win D.

Atalhos prontos do driver: Copiar `70 01 06` · Colar `70 01 19` · Recortar `70 01 1B` ·
Selecionar tudo `70 01 04` · Novo `70 01 11` · Salvar `70 01 16` · Imprimir `70 01 13` ·
Abrir (Win+E) `70 08 08` · Desfazer `70 01 1D` · Fechar janela (Alt+F4) `70 04 3D` ·
Área de trabalho (Win+D) `70 08 07` · Executar (Win+R) `70 08 15` · Minimizar `70 08 51` ·
Maximizar `70 08 52`.

Multimídia: Media Select `0x0183` · Anterior `0x00B6` · Próxima `0x00B5` · Parar `0x00B7` ·
Play/Pause `0x00CD` · Mudo `0x00E2` · Volume+ `0x00E9` · Volume− `0x00EA` · E-mail `0x018A` ·
Calculadora `0x0192` · Meu Computador `0x0194`.

Modo de repetição da macro: `2` = repetir N vezes **[testado]**. **Não use o modo `0`** **[testado]**:
a macro repete para sempre — nem soltar nem apertar de novo interrompe, só desligando o mouse.
O modo `1` não foi testado (no driver ele seria "repetir até apertar de novo").

## 7. Macros (`0x0E`)

Cada evento ocupa 3 bytes:

```
byte 0   bit 7 = soltar (1) / apertar (0) · bits 6–4 = tipo · bits 3–0 = atraso[11:8]
byte 1   atraso[7:0]   → espera em ms DEPOIS do evento (0–4095)
byte 2   código
```

| Tipo | Evento | Código |
|---|---|---|
| 1 | botão do mouse | máscara (`01` esq, `02` dir, `04` meio, `08` voltar, `10` avançar) |
| 2 | modificador | máscara de modificador |
| 3 | tecla | uso HID |
| 4 / 5 / 6 | mover X / mover Y / roda | deslocamento **[inferido]** |

Cada macro é enviada separadamente; seus eventos são concatenados e divididos em blocos de até
25 bytes:

```
byte 5 = (índice_do_bloco << 4) | total_de_blocos      (máx. 15 blocos = 125 eventos)
byte 6 = 1 + bytes_de_eventos_neste_bloco              (máx. 0x1A)
byte 7 = slot da macro (1-based, o mesmo usado na ação 0x90)
byte 8..32 = eventos
```

## 8. Arquivos do driver (referência)

- `config/default0..4.psf` — perfis (UTF-16, formato INI). Campos como `XYSetting1Value` (índice
  na tabela de DPI), `DPIOffOnFlagValue` (máscara), `PollingRateValue`, `Lighting1*`, `Key0..5Value`
  (os 3 bytes da ação) e `AllAction` (macro em 4 bytes por evento: `[VK, 0x80 se soltar, 0, 0]`
  ou `[03, atraso 24 bits]` para atrasos).
- `config/Lan_En.txt` — textos da interface em inglês.

## 9. Implementação de referência

O protocolo está implementado em C# em `app/M800.Core`:

- `Protocol/` — montagem dos pacotes e payloads de cada comando e leitura das respostas. Não faz I/O,
  então pode ser portado para qualquer linguagem (C++, Rust, Python, Node.js…).
- `Hid/` — envio dos feature reports pela API HID do Windows.
- `Services/MouseService.cs` — fila de envio (60 ms entre pacotes), INFO periódico e notificações.
