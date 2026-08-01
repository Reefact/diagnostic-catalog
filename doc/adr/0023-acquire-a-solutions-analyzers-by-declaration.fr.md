# ADR-0023 | Acquérir les analyseurs d'une solution par déclaration, jamais par découverte

🌍 **Langues :**  
🇬🇧 [English](./0023-acquire-a-solutions-analyzers-by-declaration.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Accepted
**Proposed:** 2026-08-01
**Accepted:** 2026-08-01
**Decision Makers:** Reefact

## Context

`dcat` acquiert les analyseurs qu'un catalogue reflète depuis l'une de cinq sortes de
sources : un paquet sur un flux, un fichier de paquet sur disque, un projet, un ensemble
d'assemblages déjà construits, et une solution.

Les quatre premières nomment ce qu'il faut lire. Une solution non. Elle liste des projets
de toutes sortes — bibliothèques, tests, outillage, exemples — et seuls certains produisent
les analyseurs dont les descripteurs servent à générer un catalogue (ADR-0009). Quelque
chose doit décider lesquels, et la décision ne peut pas être évitée en les lisant tous : un
projet qui ne produit aucun descripteur n'est pas seulement inintéressant à lire, c'est un
projet dont la sortie peut ne pas exister, dont la construction peut lever, et dont
l'évaluation coûte un MSBuild lancé.

Deux signaux sont les candidats évidents pour inférer l'ensemble. Mesuré sur ce dépôt au
moment de la rédaction :

| Signal | Projets appariés | Dont produisent des règles cataloguées |
|---|---|---|
| Une référence directe à un paquet de l'API Roslyn | 6 | 1 |
| Un type dérivant de `DiagnosticAnalyzer` | 2 | 1 |

La première ligne compte une référence à l'API du compilateur dont un projet aurait besoin
pour *écrire* un analyseur, et non les paquets `Microsoft.CodeAnalysis.*` qui sont
eux-mêmes des analyseurs qu'un projet se contente d'exécuter — une distinction qu'une
heuristique lisant des noms de paquets devrait faire, et qui change le décompte.

L'unique mauvais appariement du second signal est un montage de test dont le constructeur
lève exprès, et qui existe précisément pour exercer la façon dont l'outil survit à un
analyseur impossible à construire.

ADR-0010 enregistre qu'une règle qui disparaît de la source est reportée en `[Obsolete]`
plutôt que supprimée, parce que le compilateur d'un consommateur incorpore la valeur
`const` et que la supprimer casserait du code déjà livré. Une règle jamais lue et une règle
retirée par l'éditeur atteignent donc la sortie sous la même forme, et rien en aval — ni le
fichier émis, ni le code de sortie, ni le diff qu'un relecteur voit — ne les distingue.

Les catalogues de ce dépôt sont régénérés par des jobs planifiés (ADR-0017 publie l'outil
exactement pour cet usage). Un tel job rapporte un code de sortie et, quand quelque chose a
bougé, une pull request ; il n'a aucun lecteur devant une console.

Le générateur localise la sortie d'un projet en évaluant ses propriétés MSBuild. Cette
évaluation ne restaure rien, ne construit rien et n'écrit aucune sortie intermédiaire, ce
qui est ce qui permet de valider un catalogue contre une copie de travail sans la
perturber.

MSBuild porte déjà une propriété qui ressemble à la déclaration recherchée,
`EnforceExtendedAnalyzerRules`. Dans ce dépôt, un projet la positionne et ne déclare aucun
`DiagnosticDescriptor`.

Ce dépôt décide déjà deux autres appartenances par une déclaration dans le fichier propre
au projet plutôt que par une liste tenue ailleurs : sur quel train de release un projet est
livré (ADR-0002, ADR-0007), et si un projet de test tourne sur le plancher .NET Framework
(ADR-0001).

## Decision

Quand un catalogue est généré depuis une solution, `dcat` lit exactement les projets qui
déclarent produire des règles de diagnostic, et n'infère jamais quels projets pourraient en
produire.

## Rationale

La décision repose sur le fait que les deux erreurs ne sont pas comparables. Lire un projet
qui ne produit rien coûte une évaluation gaspillée et n'apporte aucun descripteur ; en lire
un de trop peu retire des règles du catalogue, et ADR-0010 transforme alors ce retrait en
affirmation publiée que ces règles ont été retirées. Le consommateur s'entend dire, par un
avertissement d'obsolescence que le compilateur lève sur son propre code, quelque chose qui
est faux — à propos d'un éditeur qui livre toujours la règle. Aucun signal nulle part ne le
rapporte : l'exécution réussit, et le diff ressemble exactement aux retraits amont que
l'outil est fait pour reporter correctement. Une heuristique réglée pour réduire l'erreur
inoffensive augmente nécessairement l'erreur nuisible.

Aucun signal candidat n'est assez proche pour que cet échange vaille la peine. Les deux
sont faux d'un facteur, et dans des sens opposés, sur la seule solution dont la réponse est
déjà connue. Les mauvais appariements ne sont pas non plus du bruit qu'un filtre pourrait
retirer : le montage qui dérive de `DiagnosticAnalyzer` est un type délibérément
inconstructible, si bien que la plus précise des deux heuristiques sélectionne, en unique
faux positif, l'entrée exacte qui met à terre le worker de lecture des descripteurs. Une
heuristique dont les échecs se concentrent sur les cas pathologiques n'est pas une
heuristique qu'il faut affiner.

L'objection la plus profonde est que l'exactitude d'une heuristique ne peut pas être
évaluée là où elle sert. La mesurer ici, c'est la mesurer sur la seule solution dont la
bonne réponse est connue ; les solutions contre lesquelles elle tournera réellement
appartiennent à des gens qui ne peuvent pas la vérifier, parce que la vérifier signifie
savoir déjà quels de leurs projets produisent des règles. Une inférence est donc
invérifiable par la seule personne en position de remarquer qu'elle s'est trompée.

La déclaration met la réponse au seul endroit qui ne peut pas se périmer. Un projet est
créé, déplacé, renommé et reciblé au cours de sa vie, et chacun de ces événements invalide
une liste tenue ailleurs tout en laissant le fichier propre au projet correct par
construction. C'est le même argument qui décide de l'appartenance à un train de release, et
la défaillance qu'il prévient est la même : un projet absent d'une liste est silencieusement
absent de ce que la liste gouverne.

La déclaration est aussi ce qui rend le refus possible, et le refus est porteur. Un outil
qui infère ne peut jamais rapporter « vous ne me l'avez pas dit » — il ne peut rapporter que
ce qu'il a trouvé, et ne rien trouver est indiscernable de l'absence de quoi que ce soit.
Parce que le mécanisme est une déclaration, une solution où personne n'a déclaré est une
question sans réponse plutôt qu'une réponse valant zéro, et l'exécution peut s'arrêter et
nommer la propriété. Ce refus est la façon dont un utilisateur qui n'a jamais entendu parler
de la propriété apprend qu'elle existe, ce qui compte parce qu'une propriété que personne ne
peut découvrir est une propriété que personne ne déclarera.

Le coût accepté est réel et retombe entièrement sur l'utilisateur : `--solution` ne lit
rien du tout tant que des projets n'ont pas été modifiés, et la première rencontre avec le
mécanisme est une exécution en échec. Il est accepté parce que l'alternative offerte n'est
pas « ça marche tout de suite » mais « ça marche tout de suite, et c'est parfois
silencieusement faux de la seule manière que cet outil existe pour empêcher ».

## Alternatives Considered

### Inférer depuis une référence à Roslyn

Envisagé parce que cela n'exige aucune coopération de l'utilisateur, correspond à la façon
dont les projets d'analyseurs sont réellement construits, et peut être lu depuis la même
évaluation MSBuild que l'outil effectue déjà.

Rejeté parce que ce n'est pas la même déclaration. Référencer Roslyn dit qu'un projet *lit
ou écrit du C#*, ce qui est vrai du worker propre à l'outil, d'un générateur de source, d'un
assemblage de correctifs et de chaque projet de test qui héberge une compilation. Mesuré
ici, cela sélectionne six projets pour en obtenir un, et les cinq supplémentaires ne sont
pas seulement du gaspillage : chacun est une évaluation, et certains n'ont aucune sortie
construite, si bien que l'exécution échouerait sur des projets que l'utilisateur n'a jamais
voulu inclure. L'heuristique est en outre ambiguë avant d'être inexacte, puisque décider ce
qui compte comme une référence à Roslyn suppose de séparer l'API du compilateur des paquets
d'analyseurs qui partagent son préfixe de nom.

### Inférer depuis un type dérivant de `DiagnosticAnalyzer`

Envisagé parce que c'est le signal le plus précis disponible — il nomme exactement la chose
à partir de laquelle un catalogue est généré — et parce que les descripteurs sont de toute
façon lus depuis de tels types.

Rejeté sur deux plans. C'est encore faux ici, en sélectionnant un montage de test à côté du
vrai analyseur, et le montage est délibérément inconstructible ; l'unique erreur de
l'heuristique est donc aussi l'entrée la plus susceptible d'interrompre l'exécution. Plus
fondamentalement, le lire exige la sortie du projet : décider s'il faut lire un projet
signifierait avoir déjà localisé et chargé ce que ce projet construit, ce qui soit force
`--solution` à construire — perdant la propriété qui rend la validation sûre contre une copie
de travail — soit échoue précisément sur les projets non construits qu'une commande à
l'échelle d'une solution a le plus de chances de rencontrer.

### Réutiliser `EnforceExtendedAnalyzerRules`

Envisagé parce qu'elle existe déjà, qu'elle est déjà positionnée sur les projets
d'analyseurs, et que cela n'exigerait ni nouvelle propriété ni nouvelle documentation.

Rejeté parce qu'elle répond à une autre question. Elle énonce qu'un projet doit être
*vérifié par* les règles d'écriture d'analyseurs, pas qu'il *produit* des règles à
cataloguer ; dans ce dépôt, le projet de correctifs la positionne et ne déclare aucun
descripteur. La surcharger ferait en outre signifier deux choses à une seule propriété, dont
les ensembles ne font aujourd'hui que se recouvrir, si bien qu'un utilisateur qui voudrait
légitimement l'un des comportements acquerrait silencieusement l'autre.

### Marquer l'assemblage produit avec un attribut

Envisagé parce que le dépôt enregistre déjà la provenance en métadonnées d'assemblage ; le
mécanisme serait donc familier et voyagerait avec l'artefact plutôt qu'avec le fichier
projet.

Rejeté parce que c'est circulaire pour cet usage. Lire un attribut d'assemblage signifie
avoir construit et localisé la sortie du projet, ce qui est la décision à prendre. Cela
porte la même conséquence que l'alternative précédente — soit `--solution` construit, soit il
échoue sur les projets non construits — et cela déplacerait la déclaration du fichier qu'un
développeur édite vers l'artefact qu'une build produit, ce qui est la mauvaise direction pour
une chose à laquelle un humain doit adhérer.

### Lister les projets dans le manifeste

Envisagé parce que le manifeste déclare déjà chaque autre fait à propos d'un catalogue, et
que cela n'exigerait aucun changement dans aucun fichier projet.

Rejeté parce que cela recrée la liste qui se périme. Un projet renommé ou déplacé laisse le
manifeste faux, et faux dans le sens silencieux. Cela répond en outre à une autre question
que celle posée : nommer des projets dans le manifeste est ce que `--project` fait déjà ;
cette alternative est donc `--project` avec de la syntaxe en plus, et non un support des
solutions.

### Lire chaque projet de la solution

Envisagé parce que cela ne peut pas deviner trop court, ce qui est la défaillance qui compte
le plus.

Rejeté parce que cela échoue sur les projets qu'il aurait fallu ignorer. Les projets de
test, d'outillage et d'exemples d'une solution ont des sorties qui peuvent ne pas être
construites, et chacun est une évaluation lancée ; une commande qui doit réussir sur chaque
projet d'une solution avant de pouvoir en cataloguer un est moins utilisable qu'une commande
qui demande lequel lire. Cela abandonne aussi le refus : une solution où rien ne produit de
règles serait lue comme un catalogue vide.

## Consequences

### Positive

* Un catalogue généré depuis une solution contient les règles d'exactement les projets que
  l'auteur a nommés, et ne peut pas en perdre une silencieusement au profit d'une
  heuristique qui n'aurait pas apparié.
* Le mécanisme ne peut pas se périmer à mesure que des projets sont créés, renommés ou
  déplacés, parce que la déclaration vit dans le fichier qui se déplace avec eux.
* Une solution où rien n'est déclaré est refusée avec un message qui nomme la propriété ; le
  mécanisme est donc découvrable depuis l'échec plutôt que depuis la seule documentation.
* L'acquisition depuis une solution hérite de l'acquisition de projet existante sans
  changement, y compris la propriété qu'elle ne restaure, ne construit et n'écrit rien.

### Negative

* `--solution` ne fait rien sur une solution qui n'a pas été préparée, et la première
  rencontre avec l'exigence est une exécution en échec.
* La propriété est une chose de plus qu'un auteur d'analyseurs doit connaître, et elle est
  propre à cet outil plutôt qu'une convention de la plateforme.
* Un auteur qui la déclare sur le mauvais projet obtient un catalogue faux sans
  avertissement — la déclaration supprime la devinette, pas la possibilité d'une réponse
  erronée.

### Risks

* Un utilisateur qui rencontre le refus peut le lire comme une fonctionnalité cassée plutôt
  que comme une instruction, si le message venait à ne plus nommer la propriété. Ce message
  fait donc partie de la décision plutôt que d'un détail d'implémentation, et il est couvert
  par des tests.
* Le nom de la propriété est désormais un contrat publié au même sens qu'un identifiant de
  règle : des projets dans les dépôts d'autres personnes le déclarent, le renommer les
  casserait donc silencieusement — une solution cesserait simplement de correspondre à quoi
  que ce soit et serait refusée.

## Follow-up Actions

* Documenter la propriété avant la fonctionnalité, puisque `--solution` ne lit rien tant qu'elle
  n'est pas déclarée — fait pour le guide dans `doc/guide/dcat.*.md`,
  `doc/guide/dcat-reference.*.md` et `doc/guide/catalogs-manifest.*.md`.
* Aucune contraignante. Le README du paquet de l'outil porte déjà le raisonnement sous forme
  brève ; si cet enregistrement est accepté, c'est l'endroit où mettre un lien une fois que
  ce lien pointerait vers une décision acceptée plutôt que proposée.

## References

* [ADR-0001](0001-floor-the-libraries-on-net-framework-4-7-2.fr.md) — le plancher .NET
  Framework, dont l'appartenance est de même une déclaration dans le projet plutôt qu'une
  liste.
* [ADR-0002](0002-partition-releases-into-trains-by-commit-scope.fr.md),
  [ADR-0007](0007-depend-across-trains-through-published-packages.fr.md) — l'appartenance à
  un train de release, le même argument appliqué à ce qu'une release publie.
* [ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.fr.md) — que le contenu
  d'un catalogue vient des descripteurs, ce qui fait de l'ensemble des projets à lire la
  question à laquelle cette ADR répond.
* [ADR-0010](0010-carry-a-retired-rule-forward-as-obsolete.fr.md) — la raison pour laquelle
  une règle omise est indiscernable d'une règle retirée, et donc la raison pour laquelle
  deviner trop court est l'erreur nuisible.
* [ADR-0017](0017-publish-the-generator-as-a-cli-on-its-own-release-train.fr.md) — l'outil
  et l'usage planifié pour lequel il est publié.
* [Issue #58](https://github.com/Reefact/diagnostic-catalog/issues/58) — la dette de
  documentation qui a soulevé la question de savoir si ce raisonnement méritait un
  enregistrement.
* `eng/CatalogGen/SolutionSource.cs` — là où le raisonnement vit aujourd'hui en commentaires,
  et là où le refus est implémenté.
