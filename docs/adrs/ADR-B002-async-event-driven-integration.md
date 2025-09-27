# ADR-B002: Integração Assíncrona Orientada a Eventos entre Contextos

Status: Aceito
Data: 2025-09-25

Contexto
- Transações e Consolidações possuem requisitos distintos (escrita vs leitura/agregação).
- Integração natural por evento de domínio TransactionCreated.

Decisão
- Adotar Event-Driven Architecture (RabbitMQ) para integração entre bounded contexts.
- Exchange topic `cash-flow-exchange` e routing key `transaction.created`.
- Consumidor atual com DLQ configurada. Publisher sem confirms e sem persistência (melhoria planejada).

Alternativas Consideradas
- Sincrono (REST entre serviços): acoplamento temporal e impacto em latência/throughput.
- Event Streaming (Kafka): maior complexidade operacional para o escopo atual.

Consequências
- Desacoplamento temporal, resiliência a falhas do consumidor, consistência eventual.
- Necessidade de governança de contratos de eventos e observabilidade de filas.

