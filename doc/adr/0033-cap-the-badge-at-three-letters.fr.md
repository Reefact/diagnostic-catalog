# ADR-0033 | Plafonner le badge à trois lettres, en abrégeant le préfixe s'il est plus long

🌍 **Langues :**  
🇬🇧 [English](./0033-cap-the-badge-at-three-letters.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Superseded by [ADR-0035](0035-badge-a-shared-prefix-catalogue-with-its-subject.fr.md)
**Proposed:** 2026-08-05
**Accepted:** 2026-08-05
**Decision Makers:** Reefact

## Context

[ADR-0032](0032-badge-a-catalogues-icon-with-its-rule-prefix.fr.md) a réglé ce que dit le badge de
l'icône d'un catalogue : le préfixe des règles qu'il reflète, jamais le nom de l'éditeur. Elle a
justifié ce choix par ce que fait le lecteur — il tient un identifiant de règle que le compilateur
vient d'afficher et cherche sur nuget.org le paquet qui le résout — et par la taille à laquelle il
rencontre l'icône, soit les 128px d'une liste.

Cet enregistrement comptait quatre catalogues, de préfixes `S`, `CA`, `IDE` et `SA`. Quatre autres
ont été publiés depuis : `IL` pour les avertissements de trimming, et `xUnit`, `NUnit` et `MSTEST`
pour les trois analyseurs de frameworks de test. Tous les quatre ont suivi ADR-0032 exactement :
leur badge porte leur préfixe de règles et non le nom de leur éditeur, qui pour les frameworks de
test se trouve être la même chaîne.

Le badge est composé plus petit à mesure que le mot s'allonge, afin de dégager les coins arrondis de
la plaque. C'était déjà vrai des quatre premiers et c'est consigné dans le gabarit. Mesuré sur les
huit icônes publiées, en hauteur de capitale dans l'artefact de 512px puis aux 128px d'une liste :

| badge | à 512px | à 128px |
| --- | --- | --- |
| `S`, `IL` | 68px | 17,0px |
| `CA`, `SA` | 48px | 12,0px |
| `IDE` | 39px | 9,8px |
| `xUnit` | 27px | 6,8px |
| `NUnit` | 26px | 6,5px |
| `MSTEST`, composé `MSTest` | 19px | 4,8px |

Rien ne mesure ni ne borne cette réduction. `PackageIconTests` affirme que deux catalogues ne
publient pas la même icône ; `tools/icon/check-icon-template.py` affirme que chaque icône dessine la
marque et le dégradé de la famille. Ni l'une ni l'autre ne lit le badge, par le choix délibéré que
consigne ADR-0032.

La marque elle-même n'a pas dérivé sur ces huit icônes, ni les formes des lettres : les glyphes
qu'un ancien et un nouveau badge partagent se recouvrent à 0,82–1,00 une fois normalisés, contre
0,36–0,66 pour deux lettres différentes du même badge. Seule la taille de composition varie, d'un
facteur 3,6.

## Decision

Le badge d'un catalogue porte au plus trois lettres, en abrégeant le préfixe de règles lorsque
celui-ci est plus long.

## Rationale

ADR-0032 justifiait le contenu du badge par ce qu'il fait à 128px, et l'une des huit icônes ne le
fait plus. À 4,8px de hauteur de capitale, un mot de six lettres est une tache aux proportions du
texte : il ne se lit pas `MSTEST` et ne distingue pas ce paquet des deux voisins, dont les badges
sont eux aussi des mots longs, dans la même graisse et à une taille voisine. Le raisonnement qui a
préféré le préfixe au nom de l'éditeur est celui-là même qui en borne aujourd'hui la longueur —
l'enregistrement précédent n'a simplement jamais eu à énoncer cette borne, aucun préfixe qu'il
couvrait ne dépassant trois caractères.

Trois est là où la mesure place le plancher, non où une préférence le placerait. Le badge de trois
lettres déjà en service, `IDE`, se compose à 9,8px : c'est petit, mais cela se lit. Le palier de
quatre lettres n'est pas représenté parmi les huit ; ceux de cinq et six sont à 6,8px et en dessous,
et ne se lisent pas. Plafonner à la dernière taille qui fonctionne est ce qui maintient la règle
dérivée du support plutôt que du goût.

Abréger vaut mieux que les alternatives parce que cela préserve ce qu'ADR-0032 protégeait. Le badge
répond toujours depuis les règles et non depuis l'éditeur : `XU` est ce qui reste de `xUnit` une
fois le plafond appliqué, pas un rendu du nom du produit, et un lecteur qui tient `xUnit1000` le
reconnaît pour la même raison que `SA` fonctionne pour `SA1000`. L'abréviation perd de
l'information, et cette perte est le prix de la lisibilité : un badge exact que personne ne peut
lire transmet strictement moins qu'un badge raccourci qui se lit.

Le plafond donne enfin à la convention ce qu'elle n'avait pas : une propriété qu'une vérification
peut affirmer sans lire de glyphes. La hauteur de capitale se mesure sur les pixels de l'icône, et
un badge d'au plus trois lettres a un plancher en dessous duquel il ne peut être composé. Cela
referme, au moins pour la longueur, la brèche qu'ADR-0032 laissait délibérément ouverte — les
lettres elles-mêmes reposent toujours sur la revue.

## Alternatives Considered

### Garder le préfixe entier et accepter la taille qu'il impose

`MSTEST` est le nom réel des règles, et tout raccourci est un second nom pour la même chose — une
chaîne de plus à apprendre pour le lecteur, et que la page du paquet n'imprime nulle part.

Rejeté parce que cela défend une propriété dont personne ne peut se servir. L'exactitude à 4,8px
n'est pas une exactitude que le lecteur reçoit ; c'est une exactitude que le fichier possède.
ADR-0032 a choisi le contenu du badge en demandant ce qu'une liste peut transmettre, et la même
question, honnêtement posée, exclut un mot de cette taille.

### Composer les préfixes longs sur deux lignes

`MS` au-dessus de `TEST`, `x` au-dessus de `Unit`. Le préfixe complet survit et chaque ligne est
composée plus grande que la ligne unique ne le serait.

Rejeté parce que cela modifie la marque plutôt que le texte. Le badge est une petite plaque carrée
dont les proportions font partie de la famille, et deux lignes de texte à l'intérieur se lisent
comme un objet différent à la taille d'une liste — un bloc de texte plutôt qu'une étiquette. Le gain
est d'ailleurs moindre qu'il n'y paraît : deux lignes dans une plaque qui doit dégager son propre
rayon de coin laissent chaque ligne plus courte que ne l'est déjà la ligne unique de trois lettres.

### Agrandir la plaque pour les préfixes longs

Le badge pourrait grandir pour accueillir ce que le préfixe demande, en gardant le texte à une
taille lisible.

Rejeté parce que la taille et la position de la plaque *sont* la marque de famille, vérifiées comme
telles à chaque pull request. Un catalogue dont le badge a une autre forme que celui de ses voisins
ne porte plus la même marque, et la vérification qui garde les huit cohérentes devrait être
affaiblie pour l'autoriser — échangeant la propriété qui est vérifiée contre une qui ne l'est pas.

### Laisser chaque catalogue choisir son badge, long ou court

Le mainteneur retient ce qui se lit le mieux pour cet éditeur, au cas par cas.

Rejeté parce que c'est l'état que cet enregistrement existe pour quitter. Quatre catalogues sont
arrivés en un jour et leurs badges ont été décidés par ceux qui les dessinaient ; une règle qui s'en
remet au jugement produit exactement l'étalement mesuré plus haut, et ne donne au relecteur rien
contre quoi vérifier.

## Consequences

### Positive

* Chaque badge est lisible à la taille où on le voit réellement, ce qui est la propriété dont
  ADR-0032 argumentait sans l'assurer.
* La règle reste dérivable : le badge se lit toujours sur les règles du catalogue, si bien qu'un
  relecteur le vérifie contre la source générée plutôt que contre son goût.
* La hauteur de capitale devient affirmable depuis les pixels : la moitié « longueur » de la
  convention peut donc être vérifiée par `tools/icon/check-icon-template.py` au lieu d'être relue.
* Les trois badges longs deviennent distinguables entre eux, ce qu'à 6,8px et en dessous ils
  n'étaient pas.

### Negative

* Trois icônes publiées changent ce qu'elles disent — `xUnit`, `NUnit` et `MSTest` deviennent `XU`,
  `NU` et `MST` — et les cinq autres sont redessinées avec elles, afin que les huit sortent d'une
  seule commande plutôt que cinq d'un dessin que personne n'a consigné.
* Le badge cesse d'être le préfixe littéral pour ces trois-là. `XU` n'apparaît dans aucun
  identifiant de règle : le lecteur le reconnaît au lieu de le faire correspondre, relation plus
  faible que celle qu'ADR-0032 décrivait.
* Une abréviation doit être choisie pour chaque préfixe long, et deux éditeurs pourraient
  raisonnablement aboutir aux deux mêmes lettres. Rien ne dérive l'abréviation comme le préfixe
  lui-même l'était.

### Risks

* Les abréviations sont décidées une fois puis recopiées. Un futur catalogue dont le préfixe
  s'abrège mal — ou entre en collision avec un préfixe en service — n'a que cet enregistrement pour
  argumenter, et la collision ne serait signalée par aucune vérification.
* Le lettrage change de fonte. Celle des badges dessinés à la main n'est pas consignée et ne
  correspond à aucune des 66 disponibles ici — le meilleur candidat atteint un recouvrement moyen
  de 0,874 pour un plafond de 0,94 — si bien que n'en redessiner que trois aurait laissé trois
  fontes à côté de cinq. Les redessiner toutes les huit règle la question, au prix de déplacer les
  cinq qui allaient bien ; ce qui les remplace est la fonte que nomme le gabarit, reproductible
  sans rien installer.
* `MST` atterrit à 32px de hauteur de capitale, soit 8px sur une liste — au-dessus des 4,8px qu'il
  remplace et en dessous des 9,8px que tient `IDE`, parce que `M`, `S` et `T` sont plus larges à
  hauteur égale que `I`, `D` et `E`. Trois lettres est un plafond sur le nombre, pas un plancher
  sur la taille.

## Follow-up Actions

* Redessiner les huit icônes depuis le gabarit, afin que le badge porté et la fonte employée
  soient l'un comme l'autre les conséquences d'une commande et non d'une séance de dessin.
* Garder la table des badges de `tools/icon/render-icon.py` à côté des catalogues qu'elle nomme :
  un projet ajouté sans badge y est refusé, ce qui est ce dont cette convention se rapproche le
  plus d'une vérification de ce que disent les lettres.

## References

* [ADR-0032](0032-badge-a-catalogues-icon-with-its-rule-prefix.fr.md) — l'enregistrement que
  celui-ci supersède, dont le choix de ce que dit le badge est conservé et à qui manquait une borne
  sur sa longueur.
* [`doc/guide/packaging-a-catalogue.fr.md`](../guide/packaging-a-catalogue.fr.md) — où la règle est
  énoncée pour un lecteur qui publie son propre catalogue.
* `tools/icon/check-icon-template.py` — ce qui est vérifié d'une icône aujourd'hui, et ce qui ne
  l'est pas.
