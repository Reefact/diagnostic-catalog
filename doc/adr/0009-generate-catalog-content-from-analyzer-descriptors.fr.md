# ADR-0009 | Générer le contenu d'un catalogue depuis les descripteurs, jamais depuis la documentation

🌍 **Langues :**  
🇬🇧 [English](./0009-generate-catalog-content-from-analyzer-descriptors.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Accepted
**Proposed:** 2026-07-30
**Accepted:** 2026-07-30
**Decision Makers:** Reefact

## Context

Le dépôt livre des catalogues qui reflètent des analyseurs qu'il ne possède pas —
ceux de SonarSource, de Microsoft, du projet StyleCop.Analyzers. Un catalogue
énonce, pour chaque règle que cet analyseur signale, son identifiant et sa
catégorie.

Trois sources pourraient fournir ce contenu : la documentation de règles publiée
par l'éditeur, les fichiers de métadonnées de règles livrés dans le paquet de
l'éditeur, et les instances de `DiagnosticDescriptor` que les assemblages
d'analyseurs déclarent eux-mêmes. Seule la dernière est ce avec quoi l'analyseur
signale réellement ; les deux autres sont des artefacts parallèles maintenus à
côté.

Roslyn ne lit jamais la catégorie d'une suppression. Il apparie sur le seul
`checkId`, et le dit dans ses propres sources. Une catégorie fausse ne produit
donc ni erreur, ni avertissement, ni suppression défaillante, ni test en échec —
ni à la compilation, ni au test, ni à l'exécution, jamais. Il n'existe aucun
moment ultérieur où l'erreur apparaîtrait.

Les catégories ne sont pas devinables. `SonarAnalyzer.CSharp` compose ses
catégories en paires `{Gravité} {Type}`, si bien que `S1144` est déclarée
`"Major Code Smell"` — une chaîne qu'aucune lecture de la documentation de la
règle ne produit, puisque la page traite gravité et type de règle séparément et
jamais sous cette forme combinée.

Un identifiant faux échoue différemment : la suppression est simplement morte, le
diagnostic continue d'être signalé, et rien n'en nomme la cause.

Toute la proposition d'un catalogue est qu'un consommateur n'ait pas à chercher
ces valeurs.

## Decision

Un catalogue généré dérive son contenu des instances de `DiagnosticDescriptor`
que les assemblages d'analyseurs amont déclarent, jamais de la documentation
publiée par l'éditeur ni de fichiers de métadonnées de règles livrés à côté.

## Rationale

Le choix de la source est tranché par le mode de défaillance plutôt que par la
commodité. Toute source autre que les descripteurs est une transcription, et une
transcription peut diverger du code qu'elle décrit — mais ici la divergence est
indétectable. Puisque la plateforme ne lit jamais la catégorie, une valeur
correcte au moment de sa copie et fausse une version plus tard ne produit rien
d'observable à aucun point du cycle de vie d'aucun consommateur. Quand une erreur
n'a pas de symptôme, la seule exigence défendable est une source qui ne peut pas
se tromper, et les descripteurs sont cette source parce qu'ils *sont* ce avec
quoi l'analyseur signale.

L'exactitude sur cet axe ne peut pas être rattrapée en aval par les tests. Un
test asserte une valeur contre une référence, et la seule référence qui vaille
est le descripteur lui-même ; un test écrit contre la documentation est une
seconde copie de la même transcription, portant la même divergence et lui prêtant
l'apparence d'une vérification. Les tests peuvent établir que la génération est
déterministe et que rien n'a été perdu en silence, mais aucun test ne peut rendre
vrai un corpus transcrit.

Sonar montre que ce n'est pas un risque marginal. Un catalogue dérivé de la
documentation ne serait pas légèrement faux sur une queue de règles inhabituelles ;
il serait faux pour tout le jeu de règles de cet éditeur, parce que la valeur que
l'analyseur déclare est une composition que la documentation n'énonce jamais sous
cette forme. Un catalogue uniformément faux sur l'une des deux valeurs qu'il
publie est pire que pas de catalogue : il a tort avec assurance, et rien dans la
build du consommateur ne le contredit.

La crédibilité suit la même ligne. Un catalogue ne vaut la peine d'être référencé
que s'il fait autorité ; s'il est une transcription, la position honnête du
consommateur est qu'il doit le vérifier contre l'éditeur avant de s'y fier — ce
qui est exactement le travail que le catalogue a été créé pour éliminer. Il n'y a
pas de version partielle de cela : la valeur est entièrement dans la source.

Le coût accepté est qu'une génération est plus difficile que la lecture d'un
fichier. Elle doit obtenir les assemblages de l'éditeur, construire les
analyseurs qu'ils contiennent et savoir lesquels appartiennent au langage
reflété, et une version amont qui change de forme casse la génération au lieu
d'être absorbée en silence. Ce coût est payé une fois, dans un outil que ce dépôt
contrôle, au moment de la génération et jamais par un consommateur — et une
génération qui s'arrête plutôt que de deviner est le comportement que cette
décision achète.

## Alternatives Considered

### Transcrire depuis la documentation de règles publiée par l'éditeur

Envisagé parce que c'est la source qu'un humain consulte, qu'elle est complète et
à jour, qu'elle explique chaque règle, et qu'elle n'exige aucun outillage au-delà
de la lecture.

Rejeté parce que la documentation décrit une règle ; elle n'est pas la valeur que
l'analyseur déclare, et là où les deux divergent, rien ne le signale. Les
catégories composées de Sonar en sont la démonstration : la documentation n'y est
pas périmée, elle n'énonce simplement jamais la chaîne que le descripteur porte.
Une source qui peut être simultanément exacte en prose et fausse en données n'est
pas utilisable comme données.

### Lire les fichiers de métadonnées de règles que l'éditeur livre dans son paquet

Envisagé parce qu'ils sont lisibles par machine, versionnés avec le paquet, et
maintenus par l'éditeur, ce qui les rend bien plus susceptibles de s'accorder aux
descripteurs qu'une page web — et que les lire n'exige aucun chargement
d'assemblage.

Rejeté parce que « bien plus susceptible de s'accorder » est exactement la
propriété qui n'aide pas quand le désaccord est silencieux. Ces fichiers restent
un artefact parallèle généré pour les besoins propres de l'éditeur ; leur format
diffère d'un éditeur à l'autre et peut changer sous eux. Échanger une certitude
contre un substitut plausible n'est rationnel que si quelque chose en aval
attrapait la différence, et rien ne le fait.

### Maintenir les catalogues à la main, en suivant chaque version amont

Envisagé parce qu'un mainteneur qui lit une note de version comprend le
changement, peut l'annoter, et peut exercer un jugement qu'un générateur n'a
pas — et que cela n'exige aucun générateur à construire ni à maintenir.

Rejeté parce que cela fait de l'exactitude une fonction d'une attention soutenue
sur des centaines de règles par éditeur, à un rythme que ce dépôt ne contrôle pas
et ne voit pas venir, avec au bout une erreur que personne ne signalera jamais.
La maintenance manuelle est une stratégie raisonnable quand les erreurs se
remarquent ; ici elles ne se remarquent pas.

### Ne publier que les identifiants et laisser les consommateurs fournir la catégorie

Envisagé parce que cela supprime entièrement le problème — aucune catégorie
livrée, aucune catégorie à se tromper — tout en livrant l'identifiant vérifié par
le compilateur, qui transforme une règle renommée ou retirée en erreur de build.

Rejeté parce que la catégorie est l'argument pour lequel aucune autre aide
n'existe. Elle est exigée par l'attribut, jamais validée par rien, et hors du
correctif de suppression intégré d'un IDE, rien ne suggère la bonne valeur.
L'abandonner reviendrait à céder la moitié du contrat qui n'a aucune autre
réponse, et à laisser le consommateur écrire une chaîne magique à côté d'une
référence symbolique.

## Consequences

### Positive

* Le contenu d'un catalogue est ce avec quoi l'analyseur signale, par
  construction, et ne peut pas en avoir divergé.
* Régénérer contre une nouvelle version amont est un diff de faits, relisible
  comme tel, plutôt qu'un exercice de relecture.
* Une valeur fausse est un défaut dans un outil que ce dépôt possède —
  reproductible et corrigeable une fois — plutôt qu'un lapsus de transcription
  qui se répète.

### Negative

* La génération dépend du chargement et de la construction des analyseurs d'un
  tiers ; une version amont qui change de forme arrête le générateur au lieu
  d'être absorbée.
* Un catalogue ne peut porter que ce que les descripteurs déclarent ; il n'existe
  aucune source plus accueillante pour l'enrichir.
* Savoir quels assemblages d'un paquet d'éditeur appartiennent au langage
  reflété est en soi un endroit où se tromper, et s'y tromper produit une sortie
  qui a l'air complète.

### Risks

* Le générateur lit le mauvais sous-ensemble d'un paquet et produit un catalogue
  de taille plausible et discrètement incomplet. Atténuation : la génération
  rapporte chaque descripteur qu'elle exclut, avec l'identifiant et la raison, si
  bien qu'une absence inexpliquée est visible plutôt que déduite d'un décompte.
* Une catégorie ou un identifiant bouge en amont et atteint les consommateurs
  sans revue, là où aucune vérification en aval ne pourra jamais le contredire.
  Atténuation : la régénération ouvre une pull request portant le diff et ne
  publie rien d'elle-même ; la revue doit avoir lieu au seul point où le
  changement est visible.
* Un futur mainteneur ajoute un repli dérivé de la documentation pour une valeur
  que les descripteurs ne fournissent pas. Atténuation : la génération est tenue
  d'échouer plutôt que de substituer une autre source, ce qui est le même
  raisonnement qui exclut les valeurs synthétisées dans ADR-0011.

## Follow-up Actions

* Maintenir le rapport exhaustif des exclusions en exigence ferme du générateur,
  puisque c'est ce qui rend un catalogue incomplet visible.
* Maintenir l'étape de revue humaine sur la pull request de régénération ;
  aucune vérification en aval ne peut la remplacer.
* Enregistrer dans les métadonnées de chaque catalogue la version amont exacte
  qu'il reflète, pour qu'un instantané périmé soit au moins lisible depuis
  l'artefact.

## References

* [ADR-0010](0010-carry-a-retired-rule-forward-as-obsolete.fr.md) — ce qui se
  passe quand une régénération trouve une règle disparue.
* [ADR-0011](0011-redistribute-rule-facts-only-never-the-vendors-prose.fr.md) —
  ce qu'un catalogue peut livrer d'un descripteur.
* [doc/specification.fr.md](../specification.fr.md) — §3.2, §14, §14.1, et
  annexes A2 et A9.
* `eng/CatalogGen` — le générateur.
