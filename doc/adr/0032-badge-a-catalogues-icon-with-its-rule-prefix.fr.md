# ADR-0032 | Badger l'icône d'un catalogue de son préfixe de règles, jamais du nom de l'éditeur

🌍 **Langues :**  
🇬🇧 [English](./0032-badge-a-catalogues-icon-with-its-rule-prefix.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Accepted
**Proposed:** 2026-08-05
**Accepted:** 2026-08-05
**Decision Makers:** Reefact

## Context

Quatre paquets catalogue sont publiés depuis ce dépôt, chacun reflétant les analyseurs d'un tiers :
Sonar, les analyseurs .NET, les styles de code Roslyn et StyleCop. Chacun porte son propre
`icon.png` à côté de son `.csproj`, et ces icônes sont la même marque — un `C` entre crochets — avec
un badge dans le coin. L'`icon.png` du dépôt lui-même est cette marque sans badge, et
`Directory.Build.targets` la donne à tout projet qui rejoint un train de release sans porter d'icône
propre, afin qu'un tel projet publie la marque de famille plutôt que l'emplacement vide de
nuget.org.

nuget.org affiche cette icône en 128px, au-dessus du titre, dans chaque liste et chaque résultat de
recherche. À cette taille, le badge tient environ trois caractères, et c'est la seule partie de
l'icône qui diffère d'un paquet à l'autre.

Les règles que chaque catalogue contient sont nommées par un préfixe, et c'est le préfixe, non
l'éditeur, qu'un consommateur écrit : `S1144`, `CA1822`, `IDE0008`, `SA1000`, dans
`[SuppressMessage(...)]` ou après `#pragma warning disable`. Le nom de l'éditeur, lui, figure déjà à
côté de l'icône — il est dans l'identifiant du paquet, dans le titre et dans la description que
nuget.org dispose juste à côté.

Les badges en service portent `S`, `CA`, `IDE` et `SA`. Ils ont été dessinés ainsi mais la règle n'a
jamais été écrite : ce qui s'en approchait le plus était un commentaire de `Directory.Build.targets`
disant que chaque icône « montre la marque de famille avec le préfixe des règles qu'elle reflète ».
Ce même commentaire consigne pourquoi il ne liste **pas** les préfixes — une énumération dans un
commentaire vieillit, celle-ci disait « S, CA, SA » quand un quatrième catalogue est arrivé, et un
lecteur copiant l'icône d'un voisin n'avait rien pour le contredire.

`PackageIconTests` est la seule vérification du domaine. Elle fait échouer un catalogue qui ne porte
pas sa propre icône, celui dont l'icône est identique octet pour octet à celle d'un autre catalogue,
et celui qui porte encore la marque sans badge du dépôt. Ses propres remarques disent ce qu'elle ne
fait délibérément pas : elle affirme la distinction, pas le contenu, et rien en elle ne lit un
badge.

Rien d'autre dans le dépôt ne mentionnait d'icône — ni [`CONTRIBUTING.md`](../../CONTRIBUTING.md),
dont la section *Adding a catalogue* énumère les étapes qu'un contributeur oublierait autrement, ni
aucune page sous [`doc/`](..). La marque n'était pas non plus reproductible : elle existait sous
forme de quatre PNG 512×512, sans source vectorielle, sans script générateur et sans métadonnée —
une cinquième icône ne pouvait donc naître que d'un redessin à vue de la marque de famille.

Les règles d'un catalogue ne partagent pas toutes un préfixe. Le catalogue StyleCop publie 194
règles nommées `SA` et 3 nommées `SX`, et son badge porte `SA`.

## Decision

L'icône d'un paquet catalogue porte la marque de famille badgée du préfixe des règles que le
catalogue reflète, jamais du nom de l'éditeur.

## Rationale

Le badge est tout ce qui distingue un catalogue d'un autre à la taille où le lecteur les rencontre
réellement : ce à quoi il dépense ses trois caractères est donc la seule décision que prend l'icône.
Les dépenser sur l'éditeur répète ce que nuget.org imprime déjà à côté ; les dépenser sur le préfixe
dit la seule chose que le texte environnant ne dit pas.

Cela répond aussi à la question que le lecteur apporte avec lui. Celui qui parcourt une liste tient
un identifiant de règle — le compilateur vient de le lui afficher — et cherche quel paquet le
résout. `SA` y répond sans qu'on ouvre la page. `SC` nommerait le produit, ce que personne ne tape
et ce que personne ne cherche.

Une règle de lecture unique est ce qui rend la prochaine icône décidable sans avoir à demander. Les
catalogues sont générés depuis les analyseurs d'autrui et l'ensemble grandit en copiant un projet
existant ; une convention énoncée comme « le préfixe des règles qu'il reflète » règle le cinquième
badge à partir des règles elles-mêmes, là où « une abréviation quelconque de l'éditeur » demanderait
un jugement, et un jugement différent à chaque fois. Que la règle soit dérivable est aussi ce qui
permet à un relecteur de vérifier un badge contre le catalogue plutôt que contre son goût.

L'enregistrer est l'objet de cette entrée, plus encore que le choix lui-même. La convention était
déjà suivie ; ce qui n'existait pas, c'était un endroit où un lecteur pouvait l'apprendre — d'où le
fait que la question « pourquoi l'icône de StyleCop porte-t-elle `SA` et pas `SC` ? » n'avait aucune
réponse dans le dépôt. Une convention suivie par quatre artefacts et énoncée nulle part est à un
redessin de sa disparition, et le commentaire qui s'en approchait le plus est — à juste titre — le
seul endroit qui refuse d'énumérer les préfixes.

La décision s'arrête délibérément avant la vérification qui l'imposerait, ce qui s'écarte de la
façon dont ce dépôt tranche d'ordinaire une règle et mérite d'être énoncé comme un choix plutôt que
laissé comme un manque. Lire un badge, c'est reconnaître des glyphes dans une image matricielle, ou
faire confiance à une déclaration écrite à côté de l'icône ; la première est une mécanique fragile
visant un défaut dont le coût est un redessin, la seconde est une deuxième chose à maintenir vraie,
ce qui est précisément le mode de défaillance que le commentaire de `Directory.Build.targets` évite
déjà. Ce qu'une comparaison octet pour octet peut affirmer honnêtement, c'est que deux catalogues ne
se ressemblent pas, et c'est ce qu'affirme `PackageIconTests`. Les lettres reposent sur cet
enregistrement et sur la revue : c'est le marché accepté.

## Alternatives Considered

### Badger l'icône du nom de l'éditeur

`SC` pour StyleCop, `CA` ou `MS` pour les analyseurs .NET, `SQ` pour Sonar. Cela nomme ce dont le
paquet *parle*, c'est ce que l'on devinerait depuis l'identifiant du paquet, et cela ne demande
aucune connaissance des règles.

Rejeté parce que cela fait doublon avec son voisin. L'éditeur est déjà dans l'identifiant, le titre
et la description que nuget.org place à côté de l'icône : le badge répéterait donc le seul fait que
le lecteur possède et retiendrait celui qui lui manque. C'est en outre indécidable dans le cas
général : les abréviations de deux éditeurs peuvent entrer en collision, et un éditeur dont le nom
n'a pas de forme courte laisse le badge au goût de chacun.

### Dessiner à chaque catalogue une icône propre, sans marque commune

Chaque paquet recevrait une icône conçue pour lui, libre de toute grammaire familiale, et
`PackageIconTests` les garderait distinctes comme elle le fait aujourd'hui.

Rejeté parce que cela transforme chaque nouveau catalogue en travail de design et perd la lecture
qu'offre la famille. Quatre paquets publiés d'un même dépôt, reflétant quatre éditeurs, se
reconnaissent aujourd'hui comme un ensemble d'un coup d'œil ; des icônes dessinées séparément ne
diraient rien de ce qu'ils ont en commun, et la distinction — la seule propriété qu'une vérification
puisse affirmer — serait la seule qui resterait.

### Imposer le badge, en lisant ses lettres ou en les déclarant à côté de l'icône

La convention pourrait être vérifiée plutôt que relue : reconnaître les glyphes dans le PNG, ou
committer un `icon.txt` à côté et le comparer au préfixe que la source générée du catalogue emploie
réellement.

Rejeté pour l'instant, sur le coût et non sur le principe. La reconnaissance de glyphes est une
dépendance lourde et fragile visant un défaut qui coûte un redessin et que la revue attrape ; un
fichier déclaratif est un second artefact qui peut lui-même être faux, et il affirmerait que le
fichier dit `SA`, jamais que l'image le dit. Si un mauvais badge atteignait un jour la revue, le
fichier déclaratif deviendrait le moins cher des deux et cette décision mériterait d'être revue.

### Badger l'icône de tous les préfixes que le catalogue publie

L'icône de StyleCop porterait `SA/SX` plutôt que `SA`, et le badge serait vrai du paquet entier
plutôt que de sa majeure partie.

Rejeté parce que cela ne survit pas à la taille pour laquelle il est dessiné. Trois caractères,
c'est ce que le badge tient en 128px ; `SA/SX` en fait cinq et serait composé assez petit pour être
illisible exactement là où l'icône fait son travail. Le préfixe majoritaire est celui qu'un lecteur
reconnaît, et la page du paquet — où l'on se rend une fois que l'icône a fait son office — énonce
l'ensemble complet.

## Consequences

### Positive

* Le badge d'un nouveau catalogue découle des règles qu'il reflète : il est décidé plutôt que
  discuté, et un relecteur peut le vérifier contre la source générée.
* La vraie question du lecteur — quel paquet résout l'identifiant que j'ai sous les yeux — trouve sa
  réponse à la taille d'une liste, sans qu'on ouvre la page.
* La famille reste lisible comme famille : une marque, une grammaire, et une seule chose qui varie.
* La règle est désormais écrite là où chaque public la rencontre : la liste du contributeur dans
  `CONTRIBUTING.md`, la page du lecteur dans le guide d'empaquetage, et la source de la marque
  elle-même.

### Negative

* Un catalogue dont les règles portent plus d'un préfixe est badgé du majoritaire : son badge
  sous-estime donc ce que contient le paquet — le `SA` de StyleCop ne dit rien de ses trois règles
  `SX`.
* Rien n'impose les lettres. Un mauvais badge se publie exactement aussi facilement qu'un bon, et
  seule la revue se tient entre les deux.
* La convention est maintenant énoncée à plus d'un endroit, et les copies peuvent diverger sans que
  rien ne le signale.

### Risks

* Un éditeur dont les règles ne portent pas de préfixe distinctif, ou un préfixe déjà employé,
  laisse la règle sans rien d'où dériver — et le repli est le nom d'éditeur que cet enregistrement
  rejette.
* Les quatre badges en service ne sont pas reproductibles lettre pour lettre. La fonte dans laquelle
  ils ont été composés n'est pas consignée : une cinquième icône peut donc reproduire la marque
  exactement et les formes des lettres seulement approximativement, et la famille dérive de la
  largeur de cet écart.

## Follow-up Actions

* Committer la marque de famille comme source vectorielle dont la seule variable est le texte du
  badge, afin qu'une nouvelle icône soit une modification et non un redessin.
* Énoncer la règle dans `CONTRIBUTING.md`, parmi les étapes *Adding a catalogue*, et dans
  [le guide d'empaquetage](../guide/packaging-a-catalogue.fr.md) pour le lecteur qui publie le sien.
* Rouvrir la question de la vérification si un mauvais badge atteint un jour la revue, en partant du
  fichier déclaratif plutôt que de la reconnaissance de glyphes.

## References

* [ADR-0004](0004-state-the-coding-rules-where-an-agent-can-act-on-them.fr.md) — une règle est
  consignée là où celui qui doit la suivre la rencontrera, plutôt que laissée à l'attention.
* [ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.fr.md) — les préfixes de règles
  que cet enregistrement badge sont lus depuis les analyseurs eux-mêmes, non recopiés.
* [`doc/guide/packaging-a-catalogue.fr.md`](../guide/packaging-a-catalogue.fr.md) — ce que nuget.org
  montre d'un paquet, et où cette règle est énoncée pour un lecteur.
* `tests/DiagnosticCatalog.Catalogs.UnitTests/PackageIconTests.cs` — la vérification qui existe, et
  son propre récit de ce qu'elle n'affirme délibérément pas.
