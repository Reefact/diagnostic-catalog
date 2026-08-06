# ADR-0038 | Arrêter les analyzers au projet qui référence un catalogue

🌍 **Langues :**  
🇬🇧 [English](./0038-stop-the-analyzers-at-the-project-that-references-a-catalogue.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Accepted
**Proposed:** 2026-08-06
**Accepted:** 2026-08-06
**Decision Makers:** Reefact

## Contexte

L'[ADR-0037](0037-ship-the-analyzers-inside-the-foundation-package.fr.md) a replié les analyzers
`DCAT` dans `DiagnosticCatalog`, afin que référencer un catalogue suffise à recevoir les
vérifications, sans second paquet à découvrir. Il les livre dans `analyzers/dotnet/cs/`, que NuGet
résout comme un actif, et a consigné comme **risque** que « l'analyzer franchit le deuxième saut
aussi facilement que le premier ».

Ce risque a ensuite été mesuré sur de vrais paquets, hors CI. Une application référençant une
bibliothèque ordinaire, qui elle-même référençait un catalogue pour ses propres suppressions, a
échoué à compiler :

```
error DCAT0006: Reference 'DiagnosticCatalog.Sonar.SonarRule.S1144'
                instead of the string literals "Major Code Smell" and "S1144"
```

Son fichier projet ne nommait ni catalogue, ni analyzer, ni fondation. Il contenait une ligne, un
`PackageReference` vers la bibliothèque. `DCAT0006` est livré en erreur
([ADR-0027](0027-ship-the-use-site-diagnostics-as-errors.fr.md)), donc le build s'est arrêté.

L'ADR-0037 nommait la parade et la plaçait sur la bibliothèque : `PrivateAssets="all"` sur sa propre
référence au catalogue, « gratuit pour elle puisqu'elle ne doit l'attribut à personne ». Mesurée,
elle fonctionne. Mais c'est un levier tenu par quelqu'un qui n'a aucune raison de s'en saisir — un
auteur de bibliothèque ne se demande pas si ses consommateurs veulent de l'analyse — et son mode de
défaillance retombe sur un tiers qui ne peut ni l'anticiper, ni en voir la cause dans son propre
fichier projet, ni avoir choisi le moindre maillon de la chaîne.

**Un actif NuGet n'a aucune notion de distance.** La fondation est transitive pour le consommateur
d'un catalogue, et transitive à nouveau pour le consommateur de celui-ci ; rien dans le paquet ne
distingue les deux, donc aucun réglage côté producteur sur un dossier `analyzers/` ne peut servir le
premier et refuser le second. Les deux moitiés du passage dont dépend le repli — le fait même que
les analyzers atteignent un consommateur transitif — relèvent du comportement de
[NuGet/Home#13813](https://github.com/NuGet/Home/issues/13813), qui contredit la documentation de
NuGet et qu'une livraison pourrait refermer.

**MSBuild trace la ligne que NuGet ne trace pas.** Le dossier `build/` d'un paquet est importé pour
une référence **directe** et pour rien au-delà ; `buildTransitive/` est importé pour tout
consommateur. Cette asymétrie est documentée, délibérée, et c'est la raison d'être de
`buildTransitive/`.

Trois autres comportements ont été mesurés en rédigeant ce document, et chacun a modifié la
conception :

* Un paquet portant `buildTransitive/` voit son dossier `build/` **entièrement ignoré**, y compris
  pour ses consommateurs directs. Le `.nuget.g.targets` généré n'importe que le fichier
  `buildTransitive/`. Un paquet ne peut donc pas porter à la fois des actifs directs et des actifs
  transitifs, et la fondation ne peut pas se servir de `build/` pour reconnaître ses propres
  consommateurs directs.
* MSBuild évalue **toutes les propriétés avant tout item**, donc une condition de propriété ne peut
  pas lire `@(PackageReference)`. La reconnaissance doit tenir en une seule condition portée par
  l'`ItemGroup`.
* Ajouter les analyzers depuis une cible plutôt qu'à l'évaluation les tiendrait hors d'un build de
  conception, et l'IDE ne les chargerait pas.

## Décision

Les assemblages d'analyzers sont livrés dans `dcat-analyzers/`, un dossier dont NuGet ne résout
rien, et n'atteignent un compilateur que par `buildTransitive/DiagnosticCatalog.targets`, qui les
ajoute lorsque `EnableDiagnosticCatalogAnalyzers` vaut `true` ou que la fondation figure parmi les
`PackageReference` propres au projet en cours de compilation.

Chaque catalogue embarque `build/<son propre identifiant de paquet>.props`, qui pose cette
propriété. NuGet l'importe pour une référence directe et pour rien au-delà.

Un projet peut poser `EnableDiagnosticCatalogAnalyzers` lui-même, dans un sens comme dans l'autre,
et aucune des deux clauses ne l'écrase.

## Justification

La propriété achetée par le repli était *utiliser un catalogue, c'est être vérifié*. Ce qu'il
livrait était *se trouver quelque part en aval d'un catalogue, c'est être vérifié*, et la seconde
phrase parle d'une autre personne — une qui n'a rien choisi et ne peut pas voir pourquoi son build
échoue. Borner le passage à un saut est ce qui rend la phrase vraie telle qu'elle est écrite.

La borne est posée là où se trouve la connaissance. Un catalogue sait qu'il est un catalogue ; la
résolution d'actifs de NuGet l'ignore, et un auteur de bibliothèque ne sait pas ce que veulent ses
consommateurs. L'opt-in est donc embarqué par le producteur, dans chaque catalogue, dérivé du
fichier projet plutôt que déclaré dans une liste — l'argument même que tranche `ReleaseTrain`, et la
raison pour laquelle un quatorzième catalogue le portera sans que personne y pense.

Quitter le dossier `analyzers/` fait aussi quitter
[NuGet/Home#13813](https://github.com/NuGet/Home/issues/13813). L'ADR-0037 dépendait d'un
comportement non documenté et le compensait en le remesurant à chaque pull request ; ceci dépend du
comportement documenté de `build/` et `buildTransitive/`. La mesure demeure, puisque c'est elle qui
a révélé le comportement non documenté au départ.

La propriété côté consommateur n'est pas une commodité. La cinquième conséquence négative de
l'ADR-0037 était qu'un consommateur voulant les attributs sans les vérifications « ne peut plus
l'exprimer en déclinant une référence de paquet, et doit faire taire les diagnostics dans
`.editorconfig` » — un paquet, un levier, et `PrivateAssets="all"` retient `[DiagnosticRule]` avec
l'analyse. Lire une propriété restitue le levier sans scinder le paquet, et il joue dans les deux
sens : l'application à deux sauts qui *veut* les vérifications peut les demander.

## Alternatives envisagées

### Ne rien changer et documenter `PrivateAssets="all"` pour les auteurs de bibliothèques

L'empaquetage ne bouge pas, le guide gagne une section, et le levier est déjà mesuré comme
fonctionnel.

Rejetée parce que c'est une garantie qui repose sur chaque auteur de bibliothèque, partout,
connaissant une règle sur un paquet qu'il a pris pour ses propres raisons. Rien ne les énumère et
aucune vérification ne peut tenir l'ensemble pour vrai. C'est la même forme que l'alternative
« référencer le paquet d'analyzers depuis chaque catalogue » rejetée par l'ADR-0037, déplacée d'un
saut et sur des gens qui ne lisent pas la documentation de ce dépôt.

### Abaisser `DCAT0006` en avertissement

La fuite cesse de casser les builds, et un adoptant délibéré le remonte en `error` dans son
`.editorconfig`.

Rejetée parce qu'elle traite le symptôme. L'application à deux sauts serait toujours analysée par un
catalogue qu'elle n'a jamais choisi, verrait toujours des diagnostics qu'elle ne peut pas expliquer,
et n'aurait toujours aucun levier ; elle les verrait simplement en jaune. Cela abandonne aussi ce
que l'ADR-0027 achetait au consommateur qui, lui, *a* choisi, et qui est la population la plus
nombreuse. La question de la sévérité pourra être rouverte sur ses propres mérites une fois
l'auditoire ramené au bon.

### Replier une copie des analyzers dans chaque catalogue

Un catalogue devient autosuffisant, et avec le même portail `build/` le passage s'arrêterait à un
saut.

Rejetée pour la raison qui l'avait fait rejeter par l'ADR-0037, aggravée par le portail. Les
catalogues roulent sur des trains différents à des rythmes différents, donc deux d'entre eux portent
des assemblages d'analyzer de même nom de fichier à des versions différentes. NuGet les unifiait par
identité de paquet ; un portail qui les ajoute **par chemin** ne donne rien à unifier à MSBuild, le
compilateur en reçoit donc deux, et la duplication dont l'ADR-0037 s'était extrait par la mesure
revient. Garder les assemblages dans l'unique fondation est ce qui fait tenir « exactement une
instance d'analyzer » pour un consommateur de plusieurs catalogues, et cette vérification est dans
`tools/packaging/verify-consumption.sh`.

### Détecter la référence directe à un catalogue depuis la seule fondation

Aucun fichier par catalogue, donc les catalogues tiers n'ont rien à livrer.

Rejetée parce que la fondation ne peut pas savoir quels identifiants de paquet sont des catalogues.
Il lui faudrait inspecter le graphe résolu à la recherche de références directes qui dépendent
d'elle, ce qui n'est ni disponible à l'évaluation ni stable d'une version de NuGet à l'autre —
échanger un mécanisme documenté contre un mécanisme astucieux, pour épargner trois lignes à un
auteur de catalogue.

## Conséquences

### Positives

* Une application est analysée par le catalogue qu'elle référence, et par rien de plus lointain. Le
  dispositif qui cassait le build d'un inconnu ne le fait plus.
* La livraison repose sur un comportement NuGet documenté plutôt que sur
  [NuGet/Home#13813](https://github.com/NuGet/Home/issues/13813) : une version de NuGet rétablissant
  le comportement documenté la laisserait fonctionner.
* Un consommateur peut décliner l'analyse et garder l'attribut, ce qu'un paquet unique ne savait pas
  offrir, et peut aussi la réclamer de plus loin.
* Un consommateur de plusieurs catalogues reste vérifié par exactement un analyzer, à une seule
  version, parce que les assemblages restent dans l'unique paquet que NuGet unifie.

### Négatives

* Un catalogue doit désormais embarquer un fichier pour que ses consommateurs soient vérifiés. Les
  nôtres le font, par dérivation depuis le fichier projet, mais un catalogue tiers qui n'embarque
  rien laisse ses consommateurs **silencieusement** non vérifiés — le build réussit et rien ne
  signale. C'est une régression réelle pour qui publie un catalogue sans lire
  [`doc/guide/packaging-a-catalogue`](../guide/packaging-a-catalogue.fr.md).
* Les analyzers sont remis au compilateur par notre propre MSBuild plutôt que par la résolution
  d'actifs de NuGet : une erreur dans un fichier `.targets` désactive donc toute l'analyse partout,
  et en silence. Les vérifications de consommation sont ce qui sépare cela d'une livraison.
* `dcat-analyzers/` n'est une convention connue d'aucun outil. Tout ce qui lit un paquet en cherchant
  des analyzers — un consommateur de SBOM, un scanner de sécurité, un miroir — ne les trouvera pas où
  il les attend.
* `NU5100` est supprimé sur les quatre assemblages embarqués, donc l'avertissement qui attraperait un
  assemblage réellement mal placé dans ce projet est éteint.

### Risques

* Le portail est une condition MSBuild, et MSBuild n'a pas de système de types. Une faute de frappe
  dans le nom de la propriété échoue dans la direction silencieuse : pas d'analyzer, pas de
  diagnostic, pas d'erreur. Seul `tools/packaging/verify-consumption.sh` s'en apercevrait, raison
  pour laquelle il asserte désormais l'activation autant que son absence.
* `@(PackageReference)` est lu à l'évaluation pour reconnaître une référence directe à la fondation.
  Un projet qui ajoute ses références depuis une cible — rare, mais légal — ne serait pas reconnu.
* L'opt-in est embarqué sous `build/$(PackageId).props`. Un catalogue qui poserait `PackageId` après
  l'import de `Directory.Build.targets`, ou pas du tout, l'embarquerait sous le mauvais nom et serait
  silencieusement silencieux.

## Actions de suivi

* Rouvrir la question de la sévérité de
  l'[ADR-0027](0027-ship-the-use-site-diagnostics-as-errors.fr.md) sur ses propres mérites, à présent
  que la population qui voit `DCAT0006` est celle qui a référencé un catalogue.
* Décider si `dcat` doit émettre `build/<id>.props` pour un catalogue qu'il génère, afin qu'un auteur
  tiers obtienne l'opt-in sans lire le guide.
* Envisager un diagnostic `DCAT`, ou une vérification au moment du pack, pour un paquet de catalogue
  qui dépend de la fondation et n'embarque pas d'opt-in — l'unique mode de défaillance que cette
  décision rend silencieux.

## Références

* [ADR-0037](0037-ship-the-analyzers-inside-the-foundation-package.fr.md) — le repli que ce document
  conserve, le risque qu'il consignait et la conséquence négative que celui-ci inverse.
* [ADR-0027](0027-ship-the-use-site-diagnostics-as-errors.fr.md) — la sévérité qui a fait de la fuite
  un build cassé plutôt qu'une énigme.
* [ADR-0007](0007-depend-across-trains-through-published-packages.fr.md) — pourquoi les catalogues
  prennent un `PackageReference` vers la fondation et `DiagnosticCatalog.Self` un `ProjectReference`,
  ce qui explique que la règle d'opt-in reconnaisse les deux.
* [`doc/specification.fr.md`](../specification.fr.md) — §16, l'empaquetage et la transitivité qu'il
  mesure.
* [`doc/guide/packaging-a-catalogue`](../guide/packaging-a-catalogue.fr.md) — ce qu'un auteur de
  catalogue doit désormais livrer.
* `tools/packaging/verify-consumption.sh` — les dix-huit vérifications qui tiennent tout ceci, dont
  le saut qui doit marcher et celui qui ne doit pas.
