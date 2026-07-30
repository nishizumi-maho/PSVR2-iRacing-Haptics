# Procedimento de teste em hardware real

## Preparação

1. Leia o aviso e o guia de jailbreak do PSVR2 Toolkit.
2. Confirme que o Toolkit oficial funciona sozinho.
3. Feche qualquer outro aplicativo que envie rumble ao HMD.
4. Deixe o botão **PARAR VIBRAÇÃO AGORA** visível.
5. Comece com o headset fora da cabeça se isso for compatível com sua
   configuração e procedimento seguro.

## Teste da API

1. Inicie Toolkit/SteamVR.
2. Abra o aplicativo.
3. Confirme:
   - arquivo de caminho encontrado;
   - DLL carregada;
   - API inicializada;
   - driver ativo.
4. Em **Teste manual**, use 10 Hz, 40 ms, um pulso.
5. Clique em iniciar.
6. Confirme fisicamente que a vibração começa e termina.
7. Clique em parada imediata e confirme ausência de vibração.
8. Repita gradualmente com 14, 18, 21 e 25 Hz, sem ultrapassar 25.

Se qualquer pulso não terminar:

1. pressione parada imediata;
2. feche o aplicativo;
3. encerre o Toolkit/SteamVR;
4. desconecte/reinicie conforme a documentação oficial;
5. guarde o log e não continue antes de entender a falha.

## Teste de falhas

- execute o app sem Toolkit: não deve fechar nem travar;
- inicie o Toolkit depois: a detecção deve se recuperar;
- desligue haptics durante um pulso: deve aparecer `Rumble: OFF`;
- desconecte/encerre o driver durante inatividade;
- encerre o iRacing durante inatividade;
- saia do carro: nenhum evento deve ser emitido;
- feche o aplicativo após um teste: o log deve registrar OFF.

Evite provocar a perda do driver durante vibração até que o comportamento
básico esteja confirmado; a função nativa atual pode bloquear nessa corrida.

## Teste no iRacing

1. Use uma sessão offline/test drive.
2. Ative gravação JSONL.
3. Faça voltas sem bater e use zebras leves.
4. Verifique ausência de vibração contínua.
5. Passe em zebra alta e marque o evento.
6. Faça um pouso/descida forte seguro e marque.
7. Produza contato leve, médio e forte em condições controladas.
8. Compare as marcações e ajuste limiares.

Não calibre em corrida oficial. Os sensores de suspensão e rumble pitch variam
por carro; repita o teste ao mudar drasticamente de categoria.

## Evidências a guardar

- versão do Toolkit instalada;
- carro/pista;
- arquivo JSONL;
- log rotativo;
- valores de perfil;
- se zero desligou o motor;
- sensação relativa em 10/14/18/21/25 Hz;
- ocorrência de falsos positivos.
