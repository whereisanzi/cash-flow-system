# ADR-NFR-001: Observabilidade – SaaS vs Self-hosted

Status: Aceito
Data: 2025-09-25

Contexto
- A stack local (Prometheus+Grafana) atende laboratório e MVP.
- Em ambientes enterprise, é comum exigir suporte 24/7 e SLAs.

Decisão
- Recomendar migração gradual para SaaS (ex.: Grafana Cloud, Datadog, Dynatrace) para ambientes de produção.
- Manter stack self-hosted apenas para dev/teste ou quando houver time dedicado.

Consequências
- Redução de custo operacional e risco de indisponibilidade.
- Ajustes de dashboards e pipelines de métricas/logs durante migração.

