# Análise do PSVR2 Toolkit

## Escopo

- repositório: `BnuuySolutions/PSVR2Toolkit`;
- branch: `main`;
- commit analisado: `9e24e6ef475660481e8b46366aaa3cb24d0b4fde`;
- data do commit: 29/07/2026;
- versão definida em `projects/common/config.h`: driver `0.2.1`, branch conforme
  configuração de build;
- nenhuma alteração foi feita no repositório.

## Descoberta e carregamento

`CustomShareManager::setupCAPIPath()` é chamado por
`projects/psvr2_openvr_driver_ex/device_provider_proxy.cpp`. No Windows, ele
descobre a pasta da DLL que contém o gerenciador e grava o diretório em:

```text
%TEMP%\psvr2tk_capi_path.txt
```

O loader oficial em
`projects/psvr2_toolkit_capi_loader/psvr2tk_capi_loader.cpp`:

1. lê somente a primeira linha;
2. acrescenta `psvr2_toolkit_capi.dll`;
3. em Windows chama `LoadLibraryExA` com
   `LOAD_WITH_ALTERED_SEARCH_PATH`;
4. resolve os exports por `GetProcAddress`.

O aplicativo C# replica a descoberta, usa caminho absoluto com
`NativeLibrary.Load` e `NativeLibrary.GetExport`, e não depende de PATH,
registro ou `System32`.

## Assinaturas e ABI

O cabeçalho `projects/psvr2_toolkit_capi/psvr2tk_capi.h` declara:

```cpp
int  psvr2_toolkit_init();
void psvr2_toolkit_deinit();
bool psvr2_toolkit_get_driver_active();
void psvr2_toolkit_set_hmd_rumble(uint8_t rumbleHz);
```

No cliente .NET:

- `int` é `Int32`;
- `uint8_t` é `byte`;
- o `bool` C++ é marshalled como `UnmanagedType.I1`;
- todos os exports são `extern "C"`;
- calling convention adotada: Cdecl; no ABI Windows x64 a convenção é
  unificada.

Não foi necessária biblioteca intermediária C++.

## Inicialização e slots

`psvr2_toolkit_init()` cria o singleton de compartilhamento, verifica o mutex
que representa o driver ativo e tenta adquirir um slot.

| Código | Constante | Significado |
| ---: | --- | --- |
| 0 | `PSVR2TK_RESULT_OK` | inicializado |
| -1 | `PSVR2TK_RESULT_DRIVER_INACTIVE` | driver inativo |
| -2 | `PSVR2TK_RESULT_NO_SLOT` | nenhum slot livre |

`projects/libcustomshare/custom_share_manager.h` define `k_maxSlots = 8`.
`psvr2_toolkit_deinit()` libera o slot, mas não contém comando de rumble OFF.

## Caminho do comando de vibração

`psvr2_toolkit_set_hmd_rumble`:

1. cria `DriverCommand`;
2. define `type = DriverCommandType::HeadsetRumbleSet`;
3. grava um `uint8_t rumbleHz`;
4. chama `CustomShareManager::submitCommand`.

O comando entra no buffer circular compartilhado de 256 entradas. O thread do
driver em `projects/psvr2_openvr_driver_ex/command_thread.cpp` consome comandos
e, para `HeadsetRumbleSet`, chama:

```cpp
ControlCommand(true, 0x08, &rumbleHz, 1, 0, 0, 1);
```

Portanto o Toolkit continua responsável pelo IPC, thread do driver e comando
USB. Este aplicativo é apenas um cliente da C API.

## Limites comprovados e não comprovados

### Intervalo

A API aceita `uint8_t` e não aplica clamp. Logo o intervalo estrutural é
`0–255`. O aplicativo oficial `psvr2_toolkit_capi_test` apresenta um
`SliderInt` de `0` a `25`. Não há evidência pública no caminho analisado de que
valores acima de 25 sejam válidos ou seguros. O cliente limita a `0–25`.

### Zero e persistência

O teste oficial permite enviar zero. O driver encaminha o byte sem tratamento.
Não há timer, duração, envelope nem auto-off na C API ou no tratamento
`HeadsetRumbleSet`. Assim:

- a origem pública é compatível com `0 = parar`, mas o firmware que interpreta
  `0x08` não está no repositório;
- não existe desligamento automático visível;
- o aplicativo sempre encerra pulsos com zero;
- a primeira validação em hardware deve confirmar que zero realmente cessa o
  motor.

### Intensidade e frequência física

Não há intensidade. O parâmetro, estrutura e UI são denominados `rumbleHz`, mas
o código apenas encaminha o byte. Não existe calibração/medição física no
Toolkit público. Frequência solicitada não deve ser descrita como intensidade.

### Retorno e falhas

O envio retorna `void`. Se o driver já estiver inativo,
`submitCommand()` retorna sem enfileirar. Se o driver desaparecer depois da
verificação, o loop atual de espera por `isFulfilled` não possui deadline
efetivo, apesar do comentário sobre cinco segundos. Por isso o cliente:

- chama fora da UI;
- serializa as chamadas;
- aplica timeout;
- bloqueia novas chamadas depois de timeout;
- não descarrega a DLL se uma chamada nativa pode continuar executando.

### Taxa de chamadas

O thread do driver chama `popCommand(10)` e comenta execução aproximada a cada
10 ms. Isso não constitui uma garantia de 100 comandos/s nem um limite seguro.
O aplicativo usa 20 comandos não-zero/s por política própria.

### Vários clientes

Os oito slots permitem vários clientes para PCM/trigger effects. O
`HeadsetRumbleCommand` não contém slot e a função não verifica `g_slot`.
Consequentemente, qualquer cliente pode sobrescrever o rumble global. Não há
prioridade entre processos.

### Headset presente e versão

A C API não oferece export de presença do headset nem de versão. Driver ativo
não prova, isoladamente, que o HMD está conectado e aceitando rumble. A UI
mostra esse estado como indeterminado e pede teste manual.

## Jailbreak e risco

O README do Toolkit marca `Headset vibration*`; a nota do asterisco informa que
certos recursos exigem jailbreak e podem causar dano ou brick. O aplicativo:

- não faz jailbreak;
- não oferece botão/script para isso;
- não altera a instalação;
- mostra o aviso antes do uso;
- aponta o usuário aos guias oficiais.

## Fontes

- [C API header](https://github.com/BnuuySolutions/PSVR2Toolkit/blob/9e24e6ef475660481e8b46366aaa3cb24d0b4fde/projects/psvr2_toolkit_capi/psvr2tk_capi.h)
- [C API implementation](https://github.com/BnuuySolutions/PSVR2Toolkit/blob/9e24e6ef475660481e8b46366aaa3cb24d0b4fde/projects/psvr2_toolkit_capi/psvr2tk_capi.cpp)
- [Official loader](https://github.com/BnuuySolutions/PSVR2Toolkit/blob/9e24e6ef475660481e8b46366aaa3cb24d0b4fde/projects/psvr2_toolkit_capi_loader/psvr2tk_capi_loader.cpp)
- [C API test](https://github.com/BnuuySolutions/PSVR2Toolkit/blob/9e24e6ef475660481e8b46366aaa3cb24d0b4fde/projects/psvr2_toolkit_capi_test/main.cpp)
- [Shared manager](https://github.com/BnuuySolutions/PSVR2Toolkit/blob/9e24e6ef475660481e8b46366aaa3cb24d0b4fde/projects/libcustomshare/custom_share_manager.cpp)
- [Driver command thread](https://github.com/BnuuySolutions/PSVR2Toolkit/blob/9e24e6ef475660481e8b46366aaa3cb24d0b4fde/projects/psvr2_openvr_driver_ex/command_thread.cpp)
- [Toolkit README](https://github.com/BnuuySolutions/PSVR2Toolkit/blob/9e24e6ef475660481e8b46366aaa3cb24d0b4fde/README.md)
