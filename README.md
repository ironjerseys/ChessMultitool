# ChessMultitool

## Tests automatisés

Le dossier `ChessMultitool.Tests` contient des tests orientés moteur d'échecs pour limiter les régressions :

- `Perft_InitialPosition_MatchesReferenceCounts` vérifie les comptes de nœuds de référence (profondeurs 1 à 3).
- `MakeUnmakeFast_RestoresBoardStateAfterSearchTreeTraversal` vérifie que la recherche restaure bien l'état du plateau.
- `EnginePerformance_ReportsUsefulMetrics` exécute une recherche minimax et publie des métriques :
  - `nodes/s`
  - coût moyen par nœud (ns)
  - feuilles/s

### Exécution

```bash
dotnet test ChessMultitool.Tests/ChessMultitool.Tests.csproj -c Release --logger "console;verbosity=detailed"
```

Le test de performance est conçu pour rester stable en CI (assertions minimales), tout en affichant des métriques exploitables pour suivre l'évolution du moteur.
