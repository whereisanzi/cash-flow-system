# ADR-B001: Mapeamento de Capacidades de Negócio e Bounded Contexts

Status: Aceito
Data: 2025-09-25

Contexto
- O domínio de fluxo de caixa exige registrar transações, consolidar saldos e prover consultas/auditoria.
- Desejamos alinhar solução a DDD, modelando capacidades como bounded contexts coesos.

Decisão
- Capacidades de negócio principais:
  - Gestão de Transações (Movimentação Financeira)
  - Consolidação Financeira / Gestão de Saldo
  - Consulta e Relato Gerencial
  - Auditoria e Compliance
- Bounded Contexts:
  - Transaction Management (Core Domain) ← Gestão de Transações
  - Financial Consolidation (Supporting Domain) ← Consolidação/Consulta/Auditoria
- Capacidades externas (fora do nosso domínio):
  - Identity Context (Keycloak)
  - Monitoring Context (Prometheus/Grafana)

Consequências
- Separação clara de responsabilidades e evolução independente.
- Alinhamento com linguagem de negócio e decisões de arquitetura.

