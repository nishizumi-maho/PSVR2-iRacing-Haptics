# Telemetria e calibração

## Layout lido

A implementação segue o layout dinâmico do IRSDK:

- header versão 2;
- até quatro `varBuf`;
- `varHeader` de 144 bytes;
- tipos char, bool, int, bitfield, float e double;
- seleção do buffer com maior `tickCount`;
- cópia integral da linha e verificação de que o tick não mudou.

Não há offsets fixos para `LatAccel` ou qualquer outra variável. A lista é
reconstruída ao conectar.

## Variáveis e tipos

| Variável | Tipo | Uso |
| --- | --- | --- |
| `IsOnTrack` | bool | jogador dentro do carro e física ativa |
| `IsOnTrackCar` | bool | carro do jogador com física ativa |
| `IsInGarage` | bool | rejeitar carregamento/garagem |
| `IsReplayPlaying` | bool | impedir efeito involuntário em replay do sim |
| `Speed` | float, m/s | mínimo e queda de velocidade |
| `LatAccel` | float, m/s² | impulso lateral |
| `LongAccel` | float, m/s² | impacto longitudinal/frenagem |
| `VertAccel` | float, m/s² | impacto vertical; inclui gravidade |
| `VelocityX/Y/Z` | float, m/s | movimento e evidência de voo |
| `Yaw/Pitch/Roll` | float, rad | orientação/capotamento |
| `YawRate/PitchRate/RollRate` | float, rad/s | rotação rápida |
| `Brake`, `Throttle` | float, 0–1 | rejeição de frenagem normal |
| `PlayerCarMyIncidentCount` | int | evidência auxiliar |
| `PlayerTrackSurfaceMaterial` | int | materiais rumble quando presentes |
| `LF/RF/LR/RRspeed` | float, m/s | bloqueio de rodas |
| `LF/RF/LR/RRshockVel` | float, m/s | compressão e assimetria |
| `LF/RF/LR/RRshockDefl` | float, m | extensão/compressão, quando disponível |
| `TireLF/RF/LR/RR_RumblePitch` | float, Hz | presença de rumble strip |

Ride height de roda aparece como telemetria dependente e historicamente não é
garantida ao vivo; contato binário de roda não existe. Nenhum deles é requisito.

## Scores

O score de colisão combina:

- módulo do desvio lateral/longitudinal em g;
- jerk horizontal;
- desaceleração;
- velocidade angular;
- bônus pequeno por aumento de incidentes.

Frenagem com pedal/bloqueio e sua transição imediata são suprimidas sem
incidente ou rotação compatível.

O score vertical combina:

- desvio vertical em g;
- jerk vertical;
- pico da velocidade de suspensão;
- velocidade angular.

A classificação usa evidência adicional:

- rumble pitch/material para zebra;
- assimetria de suspensão para queda de roda;
- período anterior de baixa aceleração/velocidade vertical para pouso;
- pico simétrico alto para compressão severa.

## JSONL

Tipos de linha:

- `frame`: `TelemetryFrame` e resultado original;
- `marker`: frame corrente e texto da marcação.

O replay reexecuta os detectores atuais. A comparação procura categoria
compatível em uma janela de 500 ms, mostrando marcações perdidas e eventos não
marcados.

## Processo recomendado

1. Use perfil Padrão.
2. Grave cinco voltas limpas, incluindo zebras usuais.
3. Confirme que zebras leves ficam sem evento.
4. Grave impactos controlados em sessão de teste.
5. Marque imediatamente cada evento.
6. Ajuste primeiro os limiares, depois as frequências.
7. Teste carros de suspensão muito diferentes separadamente.

Os valores iniciais são hipóteses funcionais verificadas por simulação, não
calibração universal para todos os carros.
