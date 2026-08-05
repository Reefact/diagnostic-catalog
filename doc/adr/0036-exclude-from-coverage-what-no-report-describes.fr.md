# ADR-0036 | Exclure de la couverture ce qu'aucun rapport de couverture ne décrit

🌍 **Langues :**  
🇬🇧 [English](./0036-exclude-from-coverage-what-no-report-describes.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Proposed
**Proposed:** 2026-08-05
**Decision Makers:** Reefact

## Contexte

Il ne reste qu'une condition rouge au quality gate SonarQube Cloud. Les deux autres se sont
résolues lorsque les dix issues ouvertes sur `tools/icon` ont été fermées : `new_reliability_rating`
et `new_security_rating` affichent A, et `new_maintainability_rating`, la duplication et la revue
des points chauds étaient déjà au vert. Reste `new_coverage`, à **76,3 %** pour un seuil de 80.

**Un seul rapport de couverture parvient à l'analyse.** `.github/workflows/sonar.yml` le nomme :

```
/d:sonar.cs.opencover.reportsPaths="artifacts/coverage/**/coverage.opencover.xml"
```

Ce rapport est écrit par `dotnet test` et il décrit du C#. Le scanner compte pourtant chaque ligne
Python du dépôt comme couvrable, ne trouve aucun rapport qui en mentionne une seule, et les déclare
toutes non couvertes. Mesuré sur `main` à l'analyse du 2026-08-05T23:37 :

| | Lignes à couvrir | Non couvertes | Couverture |
|---|---:|---:|---:|
| Projet entier | 3 014 | 747 | 76,3 % |
| `tools/icon` (les cinq `.py`) | 532 | 532 | **0,0 %** |
| `src` | 1 334 | 45 | 90,6 % |
| `eng` | 1 144 | 166 | 82,4 % |

**532 des 747 lignes non couvertes sont du Python** — 71 % du déficit — alors que les deux arbres
de C# dépassent déjà la barre exigée par le gate. Les retirer du dénominateur porte le projet à
**86,8 %**, mesuré sur `main` une fois la décision appliquée.

**Deux chiffres de couverture se confondent aisément, et le gate n'en lit qu'un.** Cela mérite
d'être nommé, car la première estimation inscrite dans cet enregistrement les a confondus.
`line_coverage` ne compte que les lignes. `coverage` — la métrique contre laquelle la condition du
gate est écrite — mélange lignes et conditions :

```
coverage = (lignes couvertes + conditions couvertes) / (lignes à couvrir + conditions à couvrir)
```

Exclure le Python porte `line_coverage` à 91,3 % et `coverage` à 86,8 %, l'écart venant de
`branch_coverage` à 78,7 % qui tire le mélange vers le bas. Les deux dépassent 80. Seul le second
est celui que la condition lit, et c'est le chiffre sur lequel cet enregistrement s'engage.

**Le code exclu n'est pas du code non testé.** `tools/tests/test-check-icon-template.sh` l'exerce à
chaque pull request, dans le job `Test the shell tooling`, avec sept assertions : que les icônes
livrées sont bien dessinées par le gabarit, qu'une marque presque identique est rejetée, qu'un
fichier indécodable échoue au lieu d'être ignoré, qu'un candidat hors du dépôt donne lieu à un
verdict plutôt qu'à une trace d'erreur, que ce que `render-icon.py` dessine est ce que
`check-icon-template.py` accepte, et qu'un catalogue absent du registre de badges est refusé. Ce
qui manque n'est pas le test. C'est un rapport qui le dise sous une forme que Sonar lit.

Le gate est donc rouge sur un nombre qui signale un **rapport manquant** plutôt que des tests
manquants, et `0,0 %` y est l'absence d'une mesure plutôt que le résultat d'une mesure.

Cette forme a déjà été rencontrée une fois, par l'autre bout. L'[ADR-0030](0030-keep-the-usage-suite-out-of-the-sonar-analysis.fr.md)
a exclu `tests/DiagnosticCatalog.Usage` via `sonar.exclusions` — les issues *et* la couverture — en
notant explicitement que « les deux dimensions sont fausses ici […] et en exclure une laisserait
l'autre mal rapportée ». Ce raisonnement ne se transpose pas entièrement : ici une dimension est
fausse et l'autre fonctionne.

## Décision

**Le code écrit dans un langage qu'aucun rapport de couverture ne décrit est exclu de la mesure de
couverture de Sonar**, via `sonar.coverage.exclusions` dans `.github/workflows/sonar.yml`.
Aujourd'hui, cela recouvre tout le Python :

```
/d:sonar.coverage.exclusions="**/*.py"
```

`sonar.coverage.exclusions`, et non `sonar.exclusions` : les issues sont voulues et restent.

## Justification

**Zéro pour cent n'est pas une mesure.** Un chiffre de couverture répond à « quelle part de ceci les
tests ont-ils exécutée ». Pour un fichier qu'aucun rapport ne mentionne, l'analyse n'a pas répondu à
cette question — elle a enregistré qu'elle ne pouvait pas la poser. Reporter cette non-réponse dans
un seuil fait dire au seuil autre chose que ce qu'il prétend.

**Seule la couverture lit mal ce code, donc seule la couverture est exclue.** C'est la distinction
avec l'ADR-0030, et c'est la raison pour laquelle le mécanisme diffère. Là-bas, la forme rapportée
*était* l'assertion : renommer `RULE_001` pour satisfaire `S101` aurait supprimé un test, si bien
qu'aucun tri issue par issue ne pouvait converger. Ici, les issues de Sonar sur ce Python étaient
correctes et ont été traitées — dix, dont sept corrigées dans le code et trois déclinées avec le
raisonnement inscrit dans le workflow. Cette analyse fonctionne et le présent enregistrement n'y
touche pas.

**Portée par langage, non par répertoire.** La raison pour laquelle une ligne est exclue tient au
langage du rapport, non à la vocation du répertoire. `**/*.py` dit exactement cela, et se vérifie
contre l'unique ligne `reportsPaths` au-dessus. `tools/**` dirait « ce répertoire n'a pas
d'importance », ce qui est une autre affirmation, et une que le présent enregistrement ne fait pas —
la suite shell tourne en CI précisément parce que ce répertoire en a.

**Cela préserve la propriété établie par l'[ADR-0021](0021-derive-the-build-rule-set-from-the-quality-profile.fr.md) :**
une règle est soit appliquée, soit son exception est écrite, jamais discrètement absente.
L'exception est écrite deux fois, dans le workflow à côté du motif et ici.

## Alternatives envisagées

### Produire un rapport de couverture Python

Le correctif honnête, et le seul qui mesurerait réellement quelque chose. Différé plutôt que
rejeté : cela suppose d'ajouter `coverage.py` au workflow, d'alimenter
`sonar.python.coverage.reportPaths`, et de faire tourner les scripts sous un harnais qui est
aujourd'hui du POSIX sh sans dépendance au-delà d'un shell — une contrainte que
l'[ADR-0013](0013-write-the-shell-tooling-for-posix-sh-not-bash.fr.md) a posée pour de bonnes
raisons et à laquelle un harnais de couverture devrait répondre. C'est un changement avec sa propre
question de dépendance, pas une ligne dans un workflow. Le présent enregistrement ne le bloque pas ;
il retire un rouge faux pour que le gate dise quelque chose de vrai entre-temps, et nomme la
suppression qui l'accompagnera.

### Exclure `tools/**` à la place

Rejeté. Cela atteint les mêmes fichiers aujourd'hui et énonce la mauvaise raison. Un `.py` ajouté
hors de `tools/` resterait mal compté, un rapport de couverture shell ne changerait rien au sens du
motif, et la phrase qu'un lecteur futur en retiendrait — que `tools/` ne vaut pas d'être mesuré —
est contredite ailleurs dans ce dépôt, qui fait tourner cette suite en CI à une barre de zéro
constat.

### Abaisser le seuil de couverture du gate

Rejeté. Cela déplacerait la barre pour le C# également, qui est le code que le rapport décrit
réellement et celui que le seuil existe pour tenir. Le nombre n'est pas trop exigeant ; c'est le
dénominateur qui est faux.

### Laisser rouge

Rejeté, et c'est l'option la plus coûteuse. `sonar-gate.yml` est un job planifié dont toute la
raison d'être est de servir d'alarme permanente sur le gate — une alarme toujours allumée est une
alarme que personne ne lit, et la prochaine régression réelle arriverait dans un rouge que tout le
monde aurait déjà appris à ignorer.

## Conséquences

### Positives

* La condition de couverture du gate est passée de 76,3 % à **86,8 %**, et le gate est revenu au
  vert avec ses six conditions OK — mesuré sur l'analyse de `main` du 2026-08-05T23:49, la
  première après l'arrivée de ce changement.
* Le chiffre que le gate rapporte devient un chiffre sur le code que son rapport décrit.
* `sonar-gate.yml` redevient informatif : un rouge nocturne veut dire que quelque chose a changé.

### Négatives

* **La couverture Python cesse d'être rapportée du tout.** `0,0 %` était inutile comme mesure mais
  restait visible comme manque ; après cela, plus rien sur le tableau de bord ne montre que ces 532
  lignes ne sont pas mesurées. Le présent enregistrement et le commentaire du workflow en sont la
  seule trace.
* Le dénominateur perd 532 lignes, et il vaut d'être aussi direct que l'ADR-0030 l'a été sur le même
  geste : ce n'est pas une amélioration des tests. Rien n'est mieux testé après qu'avant.

### Risques

* **Un rapport de couverture Python ajouté plus tard serait supprimé par cette ligne.** L'exclusion
  écarterait silencieusement la mesure même qu'elle remplace, et rien ne vérifie cette
  contradiction. L'action de suivi ci-dessous en est le seul garde-fou.
* Un futur `.py` qui mériterait réellement d'être mesuré en est exempté dès son arrivée, sans que
  personne ne l'ait décidé.
* Le motif nomme un langage. Un script réécrit dans un autre qu'aucun rapport ne décrit —
  JavaScript, PowerShell — réintroduirait le même rouge sans enregistrement et sans motif qui le
  couvre.

## Actions de suivi

* Supprimer cette exclusion si un rapport de couverture Python est un jour branché sur l'analyse, et
  remplacer le présent enregistrement plutôt que de l'éditer.
* ~~Vérifier à la prochaine analyse de `main` que `new_coverage` dépasse 80.~~ **Fait.** L'analyse
  du 2026-08-05T23:49 donne `new_coverage` à 86,8 pour un seuil de 80, avec le gate OK sur ses six
  conditions et aucune issue ouverte.

## Références

* [ADR-0030](0030-keep-the-usage-suite-out-of-the-sonar-analysis.fr.md) — le précédent le plus
  proche, et le contraste sur lequel repose cet enregistrement : là-bas les deux dimensions lisaient
  mal le code, ici une seule.
* [ADR-0021](0021-derive-the-build-rule-set-from-the-quality-profile.fr.md) — appliquée, ou
  l'exception est écrite.
* [ADR-0013](0013-write-the-shell-tooling-for-posix-sh-not-bash.fr.md) — la contrainte sur `tools/`
  à laquelle un harnais de couverture Python devrait répondre.
* [`.github/workflows/sonar.yml`](../../.github/workflows/sonar.yml) — là où vit l'exclusion, avec
  la mesure énoncée à côté du motif.
* `tools/tests/test-check-icon-template.sh` — ce qui exerce réellement le code exclu, et pourquoi
  `0,0 %` n'a jamais été une affirmation sur les tests.
