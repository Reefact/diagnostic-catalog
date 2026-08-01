# ADR-0014 | Livrer le titre de règle de l'éditeur comme documentation d'un catalogue

🌍 **Langues :**  
🇬🇧 [English](./0014-ship-the-vendors-rule-title-as-a-catalogues-documentation.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Accepted
**Proposed:** 2026-07-31
**Accepted:** 2026-07-31
**Decision Makers:** Reefact

Remplace [ADR-0011](0011-redistribute-rule-facts-only-never-the-vendors-prose.fr.md).

## Context

[ADR-0011](0011-redistribute-rule-facts-only-never-the-vendors-prose.fr.md) a
décidé qu'un catalogue généré livre identifiants, catégories et liens d'aide, et
aucun des titres de règles, formats de message ou descriptions de l'éditeur. Elle
a envisagé de livrer les titres seuls et l'a rejeté au motif que la brièveté
n'est pas la distinction qui compte : un titre est une phrase que l'éditeur a
écrite, et plusieurs centaines d'entre eux restent son catalogue.

Sous cette décision, une règle se documente en réénonçant son propre
identifiant — `Rule S1144, category Major Code Smell` — sur la constante que le
consommateur survole précisément parce qu'il a déjà l'identifiant sous les yeux.
Rien dans cette phrase ne lui dit de quoi la règle traite.

Un `DiagnosticDescriptor` porte un titre, un format de message et une
description, et les trois ne sont pas la même sorte de texte. Mesuré sur les
trois paquets reflétés tels qu'ils sont livrés aujourd'hui :

* Un **titre** est une phrase complète nommant le sujet de la règle : 54
  caractères en moyenne pour `SonarAnalyzer.CSharp`, 153 au plus. Aucun des 967
  titres des trois paquets ne contient de saut de ligne, et aucun n'est tronqué.
* Une **description** est un paragraphe de justification : 215 caractères en
  moyenne, 677 au plus, et pour 35 des 456 règles Sonar c'est visiblement un
  extrait, se terminant sur un deux-points qui introduit une liste que le paquet
  ne porte pas. Tout le corpus de prose pèse 100 Ko contre 25 Ko pour les titres.
* Un **format de message** est un gabarit, pas une phrase : 203 des 456 règles
  Sonar portent des marqueurs remplis au moment de l'analyse, et 37 ne portent
  rien d'autre que `{0}`, leur texte étant assemblé à l'intérieur de l'analyseur,
  parfois en plusieurs phrases distinctes pour une seule règle.

`SonarAnalyzer.CSharp` compile les mêmes titres et descriptions dans un catalogue
de règles interne à son propre assemblage, identique octet pour octet à ce que
ses descripteurs déclarent sur les 456 règles. L'assemblage ne porte aucun autre
texte de règle : pas de ressource embarquée, pas de littéral de plus de 700
caractères, pas de balisage. Un consommateur qui supprime une règle Sonar a déjà
cet assemblage sur disque, puisque c'est l'analyseur qui lève le diagnostic qu'il
supprime.

`SonarAnalyzer.CSharp` ne renseigne `HelpLinkUri` sur aucune de ses 456 règles
publiées ; son catalogue n'a donc aucun lien vers lequel envoyer un lecteur. Les
catalogues des analyseurs .NET et de StyleCop le renseignent sur chaque règle.

Le quick info de Roslyn affiche `<summary>` et, via une option activée par
défaut, `<remarks>` ; un commentaire de documentation est aussi ce qu'une liste
de complétion montre pendant qu'un consommateur tape le nom d'une règle.

## Decision

Un catalogue tiers généré livre le titre de règle que son descripteur amont
déclare, en commentaire de documentation de cette règle, et continue de ne jamais
livrer les descriptions ni les formats de message de l'éditeur.

## Rationale

La ligne qu'ADR-0011 a tracée — un fait sur le logiciel d'un côté, un texte écrit
à son sujet de l'autre — est la bonne ligne, et elle est conservée. Ce qui bouge,
c'est la position du titre par rapport à elle. Un titre nomme ce sur quoi
l'analyseur signale ; c'est l'étiquette par laquelle l'outillage propre à
l'éditeur, la liste d'erreurs de l'IDE et chaque consommateur identifient déjà la
règle. Une description est différente en nature, et pas seulement en longueur :
elle argumente pourquoi la règle existe, elle est la substance de la
documentation de l'éditeur, et c'est la partie qui porte son raisonnement plutôt
que son identification. Livrer la première et pas la seconde est une ligne
applicable mécaniquement, parce que le descripteur sépare déjà les deux champs.

ADR-0011 avait raison de dire que la longueur seule ne pouvait pas porter la
distinction ; les mesures ci-dessus montrent que ce n'est pas sur la longueur
qu'on s'appuie. Un titre est complet, singulier et jamais tronqué. Une
description est l'extrait de quelque chose de plus grand, parfois visiblement
coupé en milieu de phrase — ce qui en fait, contrairement à un titre, une chose
dont ce dépôt ne pourrait jamais livrer qu'une copie abîmée. Un format de message
n'est même pas une phrase : c'est un gabarit dont le texte n'existe pas avant
qu'une exécution d'analyse le produise, et pour les règles qui ne portent qu'un
marqueur il n'existe aucune valeur unique à livrer. Les trois champs échouent
différemment à la question de la redistribution, et un seul y répond.

L'argument d'honnêteté qui accompagnait celui de licence dans ADR-0011 se résout
lui aussi différemment pour un titre. Cet argument disait qu'un miroir non
affilié portant le texte explicatif de l'éditeur se lirait comme sa documentation
et vieillirait contre ses pages sans que rien ne le signale. Un titre n'est pas
un texte explicatif : il identifie plutôt qu'il n'explique, il ne peut donc pas
être pris pour la documentation de la règle, et il vieillit exactement comme
l'identifiant — quand l'amont renomme une règle, la régénération porte le nouveau
nom dans le même diff que tout le reste. Envoyer un lecteur vers la page de
l'éditeur reste la réponse pour l'explication, et la documentation d'un catalogue
est un mauvais endroit pour en reproduire une.

Le coût de la décision précédente pesait le plus lourdement sur le catalogue sans
liens d'aide. Une constante de règle Sonar ne pouvait strictement rien dire
d'elle-même : pas de titre, et pas de page vers laquelle pointer. La conséquence
qu'ADR-0011 acceptait sous la forme « les infobulles en disent moins qu'elles ne
pourraient » était, dans ce catalogue, des infobulles ne disant rien du tout.

L'argument d'échelle survit aussi, dans le sens qui compte. Vingt-cinq kilooctets
de titres ne sont pas une reformulation du catalogue de règles de SonarSource :
leur catalogue, ce sont les règles, leur justification et leurs exemples, et ce
qui reste une fois la prose retirée est une liste de noms pour des choses que
l'analyseur signale — la même liste que ce dépôt est déjà en droit de publier
sous forme d'identifiants, avec une phrase chacun plutôt qu'un numéro.

## Alternatives Considered

### Garder ADR-0011 telle quelle

Envisagé parce que c'est la décision enregistrée, qu'elle n'exige de rouvrir
aucune question de licence, et qu'elle maintient les catalogues au contenu
défendable le plus réduit.

Rejeté parce qu'elle laisse la documentation d'une règle réénoncer l'identifiant
que le lecteur a déjà, et parce que dans le catalogue Sonar elle ne lui laisse
rien d'autre non plus — ni titre, ni lien. La distinction qu'ADR-0011 refusait de
tracer sur la brièveté peut être tracée sur la nature, et c'est ce que fait cette
décision.

### Livrer le format de message plutôt que le titre

Envisagé parce que c'est la phrase qu'un consommateur lit réellement dans la
liste d'erreurs, qu'elle est impérative là où un titre est déclaratif — `Make
this field 'private' and encapsulate it in a 'public' property.` contre `Fields
should not have public accessibility` — et qu'elle lui dit quoi faire plutôt que
ce qui ne va pas.

Rejeté parce que ce n'est pas une valeur unique par règle. Sur les 456 règles
Sonar, 203 portent des marqueurs que seule une exécution d'analyse remplit, et 37
ne portent qu'un marqueur, leurs phrases étant construites à l'intérieur de
l'analyseur et parfois plusieurs par règle. En publier une signifierait choisir
une phrase qu'aucun descripteur ne déclare, ce qui est exactement l'invention
qu'[ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.fr.md)
interdit. Une règle dont la documentation se lit `Remove the unused {0} {1}
'{2}'.` est pire qu'une qui ne se lit pas.

### Livrer le titre et la description

Envisagé parce que la description est la partie qui répond au « pourquoi »,
qu'elle est déjà sous la main, et qu'un consommateur qui la veut doit
actuellement quitter son éditeur pour l'obtenir.

Rejeté parce qu'une description est la documentation de l'éditeur au sens
qu'ADR-0011 a identifié, à quatre fois le volume, et parce que 35 des
descriptions Sonar sont tronquées dans le paquet lui-même — ce dépôt ne pourrait
pas livrer une copie complète même s'il le décidait, seulement une copie abîmée
qui se lit comme celle de l'éditeur.

### Générer la documentation sur la machine du consommateur plutôt que la livrer

Envisagé parce que le consommateur a déjà l'assemblage de l'éditeur sur disque ;
une étape de build pourrait donc produire la documentation localement à partir de
lui, ne redistribuant rien et correspondant à la version exacte contre laquelle
il compile.

Rejeté pour cette décision parce que cela répond à une question différente à un
coût bien plus élevé : cela met du chargement d'assemblage dans la build de
chaque consommateur, dépend d'un comportement de cache de documentation d'IDE que
ce dépôt ne contrôle pas, et ne livre rien à un consommateur qui lit le catalogue
sur une machine où le paquet de l'éditeur n'est pas installé. Cela reste la voie
raisonnable si les descriptions devaient être souhaitées un jour, et cette
décision ne la ferme pas.

## Consequences

### Positive

* Survoler une constante de règle dit de quoi la règle traite, dans chaque
  catalogue — y compris celui de Sonar, dont les descripteurs ne fournissent
  aucun lien d'aide et qui n'avait donc rien d'autre à offrir.
* La règle de ce qu'un catalogue livre reste mécanique et par champ ; elle peut
  donc être appliquée par le générateur plutôt que jugée règle par règle.
* Une règle renommée en amont montre désormais le renommage dans le diff de
  régénération sous forme de phrase modifiée, et pas seulement d'identifiant
  modifié.

### Negative

* Les paquets portent environ 25 Ko de titres écrits par les éditeurs,
  qu'ADR-0011 refusait de porter du tout. La question de licence est répondue par
  la ligne fait/texte d'auteur plutôt que refermée en ne portant rien.
* Un titre reformulé en amont fait désormais bouger le fichier généré ; une
  version qui ne change aucune règle peut donc encore produire un diff à relire.
* La distinction entre un titre et une description doit être énoncée catalogue
  par catalogue et ne peut pas être vérifiée par la build, exactement comme la
  ligne propre d'ADR-0011 ne le pouvait pas.

### Risks

* Un mainteneur étend le même raisonnement aux descriptions, un champ à la fois,
  au motif que la frontière a déjà bougé une fois. Atténuation : la frontière a
  bougé sur une différence de nature énoncée, enregistrée ici avec les mesures qui
  la soutiennent ; un déplacement supplémentaire exige sa propre ADR argumentant
  sa propre différence.
* Un éditeur s'oppose à ce que ses titres soient portés. Atténuation : chaque
  catalogue énonce qu'il est non officiel et non affilié, nomme la version amont
  qu'il reflète, et pointe vers la documentation propre de l'éditeur ; et la
  position est réexaminable éditeur par éditeur sans changer le générateur.

## Follow-up Actions

* Réénoncer dans la documentation destinée aux consommateurs de chaque catalogue
  ce que le paquet contient désormais et où vivent les descriptions de règles de
  l'éditeur.
* Garder la restriction par champ enregistrée avec le générateur, là où celui qui
  modifiera la génération la lira.
* Réexaminer si un éditeur publie des conditions explicites de redistribution de
  ses métadonnées de règles.

## References

* [ADR-0011](0011-redistribute-rule-facts-only-never-the-vendors-prose.fr.md) —
  la décision que celle-ci remplace.
* [ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.fr.md) —
  pourquoi une valeur qui n'a jamais été lue ne doit pas être inventée.
* [doc/specification.fr.md](../specification.fr.md) — §7.5 et §14.1.
* `eng/CatalogGen` — le générateur.
