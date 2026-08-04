# ADR-0030 | Garder la suite d'usage hors de l'analyse Sonar

🌍 **Langues :**  
🇫🇷 Français (ce fichier) | 🇬🇧 [English](./0030-keep-the-usage-suite-out-of-the-sonar-analysis.en.md)

**Status:** Proposed
**Proposed:** 2026-08-04
**Decision Makers:** Reefact

## Context

`tests/DiagnosticCatalog.Usage` n'est pas un projet de test au sens ordinaire : c'est sa
**compilation** qui est l'assertion. Toutes les autres suites compilent un extrait en
mémoire et vérifient ce que l'analyseur a signalé, ce qui prouve qu'il répond correctement
sur les entrées que quelqu'un a pensé à écrire. Cela ne répond pas à la question sur
laquelle repose une 1.0 : l'analyseur reste-t-il silencieux sur du code ordinaire qu'on ne
lui a pas montré ? Un faux positif ne se trouve pas en affirmant une attente ; il se trouve
en écrivant du code qui devrait être propre et en découvrant qu'il ne l'est pas.

Chaque fichier y est donc du code qu'un consommateur pourrait raisonnablement écrire, et le
contrat est qu'il ne produise aucun diagnostic `DCAT`. Pour rendre ce contrat lisible, le
`.csproj` du projet désactive tous les analyseurs autres que DCAT —
`EnableNETAnalyzers=false`, `AnalysisLevel=none` et
`<PackageReference Remove="SonarAnalyzer.CSharp" />` — avec le raisonnement écrit juste à
côté : les fichiers imitent délibérément les habitudes des autres, et une compilation noyée
sous les règles S enterrerait le seul diagnostic que le projet existe pour faire remonter.

**Ce réglage n'atteint pas l'analyse Sonar.** `dotnet-sonarscanner begin` attache sa propre
copie de `SonarAnalyzer.CSharp`, configurée depuis le profil qualité du serveur : le
`Remove` du projet n'a donc aucun effet sur elle. Et `sonar.yml` compile avec
`TreatWarningsAsErrors=false`, parce que le scanner a besoin que la compilation aboutisse.
Les remontées sont donc collectées et téléversées en silence. La compilation est verte en
local, verte en CI, et le tableau de bord se remplit.

Mesuré sur `main` avant cette décision, à 138 issues ouvertes :

| | |
|---|---|
| Dans `tests/DiagnosticCatalog.Usage` | **136** (98,5 %) |
| Dans `tools/tests/*.sh` | 2 (`shelldre:S1192`) |
| Dans `src/` | **0** |

Les règles sont exactement celles que la suite est faite pour déclencher : `S3400`
« déclarez une constante plutôt que cette méthode » ×99 — l'`Id` et la `Category` de chaque
déclaration de règle ; `S101` « renommez `RULE_001` » ×15 ; `S1186` méthode vide ×7 ;
`S3903` type hors d'un espace de noms nommé ×2 ; plus un cas chacun de code commenté,
d'`[Obsolete]` et de constructeur statique.

Les deux conditions rouges du portail qualité mènent au même répertoire. Les deux
remontées `S3903` sont les deux seuls **bugs** signalés du projet, ce qui porte
`new_reliability_rating` à 3 pour un seuil de 1. Et la suite apporte 162 lignes à **zéro**
couverture — elle affirme en compilant et n'est jamais exécutée —, ce qui tire le projet à
79,90 % pour un seuil de 80. Exclue, la même analyse affiche 83,74 %.

Le calendrier explique pourquoi cela est apparu comme un portail rouge plutôt que comme une
dette silencieuse : la suite a atterri les 1er et 2 août et la référence du code neuf est au
30 juillet, donc les 136 comptent comme du code neuf.

## Decision

**`tests/DiagnosticCatalog.Usage` est exclu de l'analyse SonarQube Cloud**, via
`sonar.exclusions` dans `.github/workflows/sonar.yml`, aux côtés des catalogues générés :

```
/d:sonar.exclusions="**/*.g.cs,**/DiagnosticCatalog.Usage/**"
```

`sonar.exclusions`, et non `sonar.coverage.exclusions` : les deux dimensions sont fausses
ici, les issues et la couverture, et n'en exclure qu'une laisserait l'autre mentir.

Le motif est ancré sur le nom du répertoire plutôt que sur `tests/`, afin de ne pas dépendre
de l'endroit où le scanner calcule `sonar.projectBaseDir`. Ce nom est unique dans le dépôt.

## Rationale

Le précédent est déjà dans le fichier. Les catalogues générés sont exclus parce que
« personne ne peut agir sur une issue remontée là ». La suite d'usage est le cas le plus
fort : la forme signalée **est** l'assertion. `RULE_001` n'est pas une erreur de nommage que
personne n'a pris le temps de corriger — c'est l'entrée qui prouve que `DCAT0005` ne se
déclenche pas sur un nom de cette forme. Le renommer en `Rule001` pour satisfaire `S101`
n'améliorerait pas le code : cela supprimerait un test.

Il ne s'agit donc pas du geste habituel qui consiste à faire taire une remontée gênante.
Les remontées ont raison sur le code et tort sur ce à quoi ce code sert, et aucun tri
issue par issue ne corrige cela : il y en a 136 aujourd'hui et chaque fichier ajouté à la
suite en fabrique d'autres, si bien que les marquer « won't fix » sur le serveur est un
travail sans fin et sans trace dans l'arbre.

Cela préserve la propriété établie par l'ADR-0021 — une règle est soit appliquée, soit son
exception est écrite, jamais absente en silence. L'exception est écrite deux fois : dans le
workflow, à côté du motif, et ici.

**Cela ne contredit pas l'ADR-0024**, qui exige que la compilation échoue sur tout
diagnostic que le cliquet ne peut pas voir. Cette décision existe pour abolir les
*remontées sur lesquelles personne ne peut agir*, et son sujet est le code propre à ce
dépôt sous son propre cliquet. La suite d'usage est déjà hors de ce régime par une décision
antérieure explicite : son `.csproj` désactive les analyseurs et promeut
`TreatWarningsAsErrors` sans condition, ce qui rend sa compilation **plus stricte** que le
reste du dépôt sur la seule famille de règles qui la concerne. L'exclure de la vue du
serveur sert le but de l'ADR-0024 plutôt qu'il ne le sape.

## Consequences

**La suite devient invisible à Sonar.** Un vrai défaut y passerait inaperçu du tableau de
bord. C'est accepté : le projet ne livre rien, n'a ni consommateur ni exécution — il n'est
jamais lancé — et son seul contrat, qu'aucun diagnostic `DCAT` ne se déclenche, est
appliqué par sa propre compilation sur chaque poste et en CI, ce qui est une vérification
que Sonar n'effectuait de toute façon pas.

**Le portail qualité devrait repasser au vert**, puisque les deux conditions en échec
mènent ici. Le chiffre de couverture passe de 79,90 % à environ 83,74 %, et il faut être
net sur ce que cela signifie : non pas une amélioration des tests, mais le retrait de 162
lignes d'un dénominateur auquel elles n'appartenaient pas.

**Les deux issues restantes sont réelles et demeurent.** `shelldre:S1192` dans
`tools/tests/test-docs-footer.sh` et `tools/tests/test-commit-lint.sh` signale des
littéraux dupliqués dans des scripts de test. Elles n'affectent pas le portail et cette
décision n'y touche pas.

**Un renommage du répertoire le ré-inclut en silence.** L'exclusion est un motif de chemin
et rien ne la lie au projet. La panne est bruyante plutôt que dangereuse — le tableau de
bord se remplirait à nouveau —, mais elle serait rencontrée sur le tableau de bord plutôt
que dans le diff qui l'a causée.

## Follow-up Actions

* Traiter les deux remontées `shelldre:S1192` dans `tools/tests`, ou consigner pourquoi
  elles restent.
* Déplacer l'exclusion avec le répertoire si `tests/DiagnosticCatalog.Usage` est un jour
  renommé.

## References

* [ADR-0021](0021-derive-the-build-rule-set-from-the-quality-profile.fr.md) — l'ensemble de
  règles Sonar de la compilation, et la propriété que ce document préserve : appliquée, ou
  l'exception est écrite.
* [ADR-0024](0024-fail-on-any-diagnostic-the-ratchet-cannot-see.fr.md) — l'invariant sur les
  remontées sur lesquelles personne ne peut agir, que cette décision sert plutôt qu'elle ne
  le contredit.
* [`.github/workflows/sonar.yml`](../../.github/workflows/sonar.yml) — où vit l'exclusion,
  avec le même raisonnement énoncé à côté du motif.
