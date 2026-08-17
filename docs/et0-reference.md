# ET0 pendiente de validación agronómica

El Sprint 2 no incorpora un cálculo de evapotranspiración improvisado porque todavía no existe una estación meteorológica validada ni una fuente confiable de radiación, viento y humedad relativa.

La implementación recomendada para un sprint posterior es FAO-56 Penman–Monteith:

`ET0 = [0.408 Δ (Rn − G) + γ (900 / (T + 273)) u2 (es − ea)] / [Δ + γ (1 + 0.34 u2)]`

Antes de utilizarla para ordenar riegos deben definirse unidades, altura del anemómetro, control de calidad meteorológico y coeficientes de cultivo por etapa fenológica. Hasta entonces, las recomendaciones continúan basándose en humedad observada y umbrales agronómicos configurados.
