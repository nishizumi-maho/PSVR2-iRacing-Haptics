# PSVR2 iRacing Haptics

Aplicativo auxiliar independente para Windows que converte telemetria do
iRacing em padrões de vibração do headset PlayStation VR2 por meio da C API do
[PSVR2 Toolkit](https://github.com/BnuuySolutions/PSVR2Toolkit).

O programa **não incorpora, redistribui, modifica ou substitui** o PSVR2
Toolkit. Ele não altera o driver, não acessa o USB diretamente e não executa
jailbreak. A DLL continua pertencendo à instalação do Toolkit e é carregada no
local indicado pelo próprio Toolkit.

## Aviso importante

Na versão do Toolkit analisada, a vibração do headset é marcada como um recurso
que exige jailbreak. O projeto oficial alerta para risco de danificar ou até
inutilizar o headset. Este aplicativo não torna o procedimento mais seguro e
não executa nenhuma etapa dele. Leia os guias oficiais e prossiga por sua conta
e risco.

Não use outro cliente de rumble do headset ao mesmo tempo: a C API possui até
oito slots de cliente, mas o comando de vibração do HMD é global e não tem
prioridade/arbitragem entre programas.

## Requisitos

- Windows 10 ou Windows 11 x64;
- PlayStation VR2 no PC;
- PSVR2 Toolkit instalado, configurado e em execução;
- driver do Toolkit ativo;
- jailbreak/configuração exigida pelo Toolkit para vibração do headset;
- iRacing, somente para telemetria real;
- nenhum privilégio administrativo;
- nenhum Python.

O ZIP portátil é autocontido e não exige instalação do .NET. Configurações,
logs e gravações ficam na pasta `data` ao lado do executável porque o pacote
inclui `portable.mode`. Se esse arquivo for removido, os dados passam para
`%LOCALAPPDATA%\PSVR2iRacingHaptics`.

## Uso rápido

1. Instale e inicie normalmente o PSVR2 Toolkit e o SteamVR.
2. Confirme no Toolkit que o driver está ativo.
3. Extraia todo o ZIP para uma pasta comum do usuário.
4. Execute `PSVR2iRacingHaptics.exe`.
5. Abra **Teste manual**, mantenha `PSVR2 Toolkit (hardware real)` selecionado
   e teste inicialmente 10 Hz por 40 ms.
6. Use **PARAR VIBRAÇÃO AGORA** se algo não se comportar como esperado.
7. Inicie o iRacing e entre no carro. O estado deve mostrar `iRacing conectado`
   e `Usuário no carro`.

O teste manual funciona sem o iRacing. O simulador funciona sem iRacing e sem
headset: selecione `Dispositivo simulado` em **Teste manual** e ative
`Usar telemetria simulada` em **Calibração e simulador**.

## O que foi confirmado no PSVR2 Toolkit

A análise foi feita no commit
[`9e24e6ef475660481e8b46366aaa3cb24d0b4fde`](https://github.com/BnuuySolutions/PSVR2Toolkit/commit/9e24e6ef475660481e8b46366aaa3cb24d0b4fde),
estado de `main` em 29 de julho de 2026.

| Questão | Resultado confirmado no código |
| --- | --- |
| DLL | `psvr2_toolkit_capi.dll` |
| Descoberta | `%TEMP%\psvr2tk_capi_path.txt`, escrito pelo driver com o diretório da C API |
| Loader oficial | Lê a primeira linha, acrescenta o nome da DLL e usa `LoadLibraryExA(..., LOAD_WITH_ALTERED_SEARCH_PATH)` |
| Inicialização | `psvr2_toolkit_init()` retorna `0` (OK), `-1` (driver inativo) ou `-2` (sem slot) |
| Clientes | Há 8 slots; `deinit()` apenas libera o slot |
| Estado do driver | `psvr2_toolkit_get_driver_active()` retorna `bool` |
| Rumble | `void psvr2_toolkit_set_hmd_rumble(uint8_t rumbleHz)` |
| Faixa | A função aceita todo `uint8_t` e não valida. O teste oficial limita a interface a `0–25`. Este aplicativo adota esse limite conservador |
| Driver | `HeadsetRumbleSet` encaminha 1 byte ao comando de controle `0x08` |
| Intensidade | Não existe parâmetro separado |
| Duração | Não existe duração nem auto-off no caminho analisado |
| Retorno | O envio é `void`; não informa sucesso ao cliente |
| Driver inativo | O gerenciador descarta o comando quando detecta driver inativo |
| Presença do headset | Não é exposta separadamente pela C API |
| Versão da C API | Não existe export de versão |

O nome do parâmetro e a interface oficial chamam o byte de Hz, e o driver o
encaminha sem conversão. O código público, porém, não mede nem garante que a
frequência física percebida seja exatamente o número solicitado.

O teste oficial permite enviar `0`, e não há duração/auto-off no driver. Isso é
compatível com `0 = desligado`, mas o significado final do comando USB vive no
firmware do headset e não é demonstrado no repositório. Por segurança, o
aplicativo sempre envia `0` após cada pulso e requer a confirmação inicial no
teste físico.

O thread de comandos do driver trabalha em torno de 10 ms, mas o Toolkit não
documenta uma frequência máxima segura de chamadas. O limite padrão deste
aplicativo é **20 chamadas não-zero por segundo**; comandos `0` de emergência
não aguardam o limitador.

Detalhes e referências de arquivo estão em
[docs/PSVR2_TOOLKIT_ANALYSIS.md](docs/PSVR2_TOOLKIT_ANALYSIS.md).

## Telemetria do iRacing

A integração lê diretamente `Local\IRSDKMemMapFileName` e aguarda
`Local\IRSDKDataValidEvent`. Os cabeçalhos são descobertos em tempo de execução;
nenhum offset de variável de carro é fixo.

Sinais usados:

- `IsOnTrack`, `IsOnTrackCar`, `IsInGarage`, `IsReplayPlaying`;
- `Speed`, `LatAccel`, `LongAccel`, `VertAccel`;
- `VelocityX`, `VelocityY`, `VelocityZ`;
- `Yaw`, `Pitch`, `Roll`, `YawRate`, `PitchRate`, `RollRate`;
- `Brake`, `Throttle`, `Gear`, `RPM`;
- `PlayerCarMyIncidentCount`;
- `PlayerTrackSurface` e `PlayerTrackSurfaceMaterial`;
- `LF/RF/LR/RRspeed`;
- `LF/RF/LR/RRshockDefl` e `LF/RF/LR/RRshockVel`;
- `TireLF/RF/LR/RR_RumblePitch`.

Suspensão, velocidades individuais e rumble pitch dependem do carro/sessão. O
programa detecta a ausência e usa sinais alternativos. O SDK não oferece um
evento direto e confiável de colisão, contato individual de cada roda ou
impulso de dano; por isso a classificação é heurística.

O detector calcula média lenta, aceleração suavizada, desvio da média, jerk nos
três eixos, desaceleração, rotação, atividade/assimetria da suspensão e contexto
temporal. Contador de incidentes é somente evidência auxiliar.

## Padrões padrão

- batida leve: 12 Hz por 75 ms;
- batida média: 18 Hz por 125 ms;
- batida forte: 24 Hz por 145 ms, 40 ms de pausa, 21 Hz por 80 ms;
- capotamento: dois pulsos de 22 Hz por 90 ms;
- zebra forte: 13 Hz por 60 ms;
- queda de roda: 15 Hz por 80 ms;
- pouso: 18 Hz por 60 ms, 30 ms de pausa, 14 Hz por 50 ms;
- compressão severa: 20 Hz por 105 ms.

Frequência não é tratada como intensidade física. Os efeitos são diferenciados
por frequência, duração, quantidade, intervalo e cauda.

Prioridades: batida forte, capotamento, batida média, pouso, compressão, batida
leve, queda de roda e zebra. Um efeito forte cancela um fraco; um efeito fraco
não interrompe um forte.

## Segurança implementada

- botão permanente de parada imediata;
- `0 Hz` após pulso, cancelamento, exceção, desativação e encerramento;
- `0 Hz` ao perder telemetria, sair do carro ou perder o driver;
- duração contínua e duração total máximas;
- serialização de todas as chamadas;
- limite de chamadas por segundo;
- timeout da chamada nativa;
- bloqueio de novos comandos se uma chamada nativa travar;
- nenhuma operação nativa na thread da interface;
- zebras leves desativadas por padrão;
- abertura normal sem iRacing e sem Toolkit.

## Calibração e replay

1. Abra **Calibração e simulador**.
2. Clique em **Iniciar gravação**.
3. Durante a volta, marque manualmente uma batida, zebra forte ou pouso.
4. Encerre a gravação.
5. Use **Comparar marcações** para reprocessar o JSONL com os limiares atuais.
6. Ajuste os limiares e compare novamente sem precisar entrar no simulador.

Cada linha JSONL contém o snapshot completo necessário ao algoritmo, a
classificação feita na gravação e, quando aplicável, a marcação manual. A
comparação aceita uma detecção compatível até 500 ms da marcação.

Os cenários internos incluem carro parado, aceleração, frenagem, zebra leve,
zebra forte, queda de roda, pouso, batida lateral, batida frontal, colisão
forte, capotamento e perda de conexão.

## Perfis

- **Padrão**: equilíbrio inicial;
- **Suave**: limiares maiores e frequências menores;
- **Forte**: limiares menores e frequências maiores;
- **Personalizado**: valores editados na interface.

A configuração já separa perfil e detectores para permitir futuramente perfis
por carro, categoria, pista e usuário.

## Logs

No modo portátil: `data\logs\psvr2-iracing-haptics.log`.

Os logs registram versão do app, caminho/versão disponível da DLL, resultado de
init, estado do driver, conexão do iRacing, entrada/saída do carro, valores e
motivos de cada evento, padrão enviado, cancelamentos, erros e `Rumble: OFF`.
Cada arquivo gira em 5 MiB, com quatro históricos.

## Compilar

Requer o SDK .NET 8 x64.

```powershell
.\build.ps1
```

O script restaura, compila, executa os testes, publica autocontido para
`win-x64` e cria:

```text
build\PSVR2-iRacing-Haptics-v0.1.0-win-x64-portable.zip
```

Para executar pelo código-fonte no Windows:

```powershell
.\run.ps1
```

Para iniciar já com o rumble falso:

```powershell
.\run.ps1 -Simulator
```

Os testes são um executável sem dependências de framework de testes:

```powershell
dotnet run --project .\tests\PSVR2iRacingHaptics.Tests -c Release
```

## Teste em hardware real

Siga [docs/HARDWARE_TEST.md](docs/HARDWARE_TEST.md). O pacote foi compilado e
validado com simuladores, mas este ambiente não possui PSVR2 nem iRacing para
confirmar a vibração física, presença real do headset ou limiares por carro.
O resultado exato da compilação e dos testes está em
[docs/VALIDATION.md](docs/VALIDATION.md).

## Estrutura

```text
src/PSVR2iRacingHaptics.Core
src/PSVR2iRacingHaptics.Infrastructure
src/PSVR2iRacingHaptics.App
tests/PSVR2iRacingHaptics.Tests
docs
build
```

Consulte [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) para os componentes e o
fluxo de falhas.

## Licença e marcas

Este projeto usa licença MIT. PSVR2 Toolkit é um projeto externo e também
possui seus próprios termos. PlayStation, PlayStation VR2, Sony, iRacing e
SteamVR são marcas de seus respectivos proprietários. Este projeto não é
oficial nem afiliado a eles.
