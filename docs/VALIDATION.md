# Resultado da validação da versão 0.1.0

Data: 29 de julho de 2026.

## Ambiente

- SDK: .NET 8.0.423;
- alvo do aplicativo: `net8.0-windows`, `win-x64`;
- publicação: autocontida, sem trimming e sem single-file;
- ambiente de compilação: contêiner Linux;
- hardware físico indisponível: PSVR2, driver do Toolkit e iRacing.

## Compilação

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

O executável publicado foi identificado como `PE32+ executable (GUI) x86-64`
para Windows. O pacote foi inspecionado para confirmar que não contém
`psvr2_toolkit_capi.dll` nem executáveis do PSVR2 Toolkit.

## Testes automatizados

```text
Resultado: 25/25 testes aprovados.
```

Os testes cobrem persistência e validação de configuração, filtro e jerk,
rejeição de aceleração e frenagem normais, zebra leve, colisões lateral,
frontal e forte, capotamento, pouso, queda de roda, telemetria inválida,
mapeamento de efeitos, prioridade e preempção, envio de `0 Hz`, cancelamento,
parada de emergência, dispositivo indisponível, gravação/replay/calibração e
ausência segura do Toolkit e do iRacing.

## Limite da validação

O aplicativo WinForms não pôde ser iniciado neste ambiente Linux e nenhuma
vibração física foi comandada. Portanto, ainda dependem de Windows e hardware
real:

- confirmação de que `0` desliga o motor no firmware usado;
- sensação e correspondência física dos valores chamados de Hz;
- detecção separada de headset conectado;
- comportamento durante perda real do driver;
- ajuste dos limiares por carro e pista;
- convivência com outros clientes da C API.

O roteiro reproduzível está em `docs/HARDWARE_TEST.md`.
