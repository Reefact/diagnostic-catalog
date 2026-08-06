# ADR-0040 | Classer chaque diagnostic DCAT par ce qu'il dit, pas par le public auquel il s'adresse

🌍 **Langues :**  
🇬🇧 [English](./0040-grade-every-dcat-diagnostic-by-what-it-says.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Accepted
**Proposed:** 2026-08-06
**Accepted:** 2026-08-06
**Decision Makers:** Reefact

## Contexte

Treize identifiants `DCAT` sont livrés, et leurs sévérités par défaut ont été fixées une par une.

L'[ADR-0027](0027-ship-the-use-site-diagnostics-as-errors.fr.md) a promu `DCAT0001`, `DCAT0006` et
`DCAT0007` en `Error` et maintenu tout le reste en `Warning`, sur un argument de **public** : ces
trois-là sont ce pour quoi un consommateur référence un catalogue, tandis que `DCAT0002`–`DCAT0004`,
`DCAT0011`–`DCAT0013` s'adressent à qui *écrit* un catalogue, ce qu'il décrivait comme « un public
différent avec une build différente ». Il a maintenu `DCAT0009` en `Warning` sur un second argument :
la vérification manque un identifiant atteint au travers d'une constante, donc promouvoir une règle
qui sous-détecte « fait échouer les builds de façon inégale ».

L'[ADR-0039](0039-require-a-justification-on-every-suppression.fr.md) a ensuite ajouté `DCAT0014` et
l'a livré en `Warning`, en s'écartant explicitement de l'ADR-0027 : la règle était neuve, elle signale
des suppressions par ailleurs entièrement correctes, et une erreur aurait rencontré une base de code
entière d'un coup. Il a inscrit « revoir la sévérité dans une release » en action de suivi.

`DCAT0015` est arrivé en dernier et sa sévérité n'est consignée nulle part sinon dans un commentaire
de `Descriptors.cs`, qui reprend par analogie l'argument de public de l'ADR-0027 et ajoute qu'il est
le seul diagnostic à lire un fait extérieur à la compilation.

Ce que chaque identifiant signale réellement, en faits :

* `DCAT0001` — les deux arguments nomment deux règles différentes ; la suppression se résout et fait
  taire autre chose que ce qu'elle prétend.
* `DCAT0002`–`DCAT0004` — un type marqué `[DiagnosticRule]` manque le contrat structurel du §8 : ce
  n'est pas une classe statique non générique, ou il n'expose pas de `const string Id` publique, ou
  pas de `const string Category` publique. Une règle dans cet état ne publie rien qu'une suppression
  puisse nommer.
* `DCAT0005` — l'identifiant porte un caractère que C# interdit dans un nom de type, donc le nom du
  type est l'identifiant légalisé et aucune orthographe plus proche n'existe. Il n'y a rien à réparer.
* `DCAT0006` — des littéraux qu'un catalogue présent dans la compilation pourrait remplacer par des
  références vérifiées.
* `DCAT0007` — une suppression à moitié migrée : un argument est une référence, l'autre une valeur.
* `DCAT0009` — un `UnconditionalSuppressMessage` dont l'identifiant n'est pas un avertissement IL. Le
  décodeur d'ILLink l'écarte et Roslyn ne lit jamais cet attribut : la ligne n'a d'effet nulle part.
* `DCAT0011` — une règle atteint sa catégorie sans passer par une constante déclarée dans une classe
  `[DiagnosticCategory]`. Elle se réduit aujourd'hui au bon littéral.
* `DCAT0012` — un identifiant de règle écrit en littéral là où `nameof` ne pourrait pas dériver. Il
  concorde aujourd'hui avec le nom du type.
* `DCAT0013` — l'identifiant est un identifiant C# valide, le type aurait pu s'appeler ainsi, et ne
  s'appelle pas ainsi. Chaque site d'utilisation lit un nom qui ne dit pas quel diagnostic il
  supprime ; la référence compile, se résout et fonctionne.
* `DCAT0014` — rien ne consigne pourquoi le diagnostic est réduit au silence. La présence est
  vérifiée ; le contenu n'est jamais jugé.
* `DCAT0015` — un paquet de catalogue publie des règles et n'embarque aucun opt-in : le référencer ne
  vérifie personne. Le paquet ne fait pas la seule chose pour laquelle il existe, et ce silence est
  indiscernable d'une base de code sans rien à signaler.

Trois faits supplémentaires pèsent sur le calendrier. Les analyzers n'ont **jamais été publiés** : la
dernière release du train `lib` est `0.1.0`, qui ne livrait que des attributs, donc aucun consommateur
n'a de build qu'un changement de sévérité puisse casser ici. Chaque sévérité reste redéfinissable par
identifiant et par chemin via un `.editorconfig` ordinaire. Et un catalogue publié par ce dépôt est
*généré*, donc `DCAT0002`–`DCAT0004` et `DCAT0011`–`DCAT0013` ne peuvent pas s'y déclencher — le
public sur lequel raisonnait l'ADR-0027 est un auteur tiers écrivant un catalogue à la main, ou
quiconque déclare des règles pour un référentiel interne.

## Décision

La sévérité par défaut de chaque diagnostic `DCAT` est décidée par **ce que le diagnostic dit du
code** — `Error` quand le contrat obligatoire de cette bibliothèque n'est pas satisfait, quand la
suppression est incorrecte ou sans effet, ou quand le paquet ne fournit pas le comportement qu'il
promet ; `Warning` quand le code fonctionne aujourd'hui et reste sujet à dérive, mal ancré ou
trompeur ; `Info` pour une exception légitime que personne ne peut réparer et qu'il est néanmoins
utile de rendre visible — et jamais par le public auquel le diagnostic s'adresse.

## Justification

**Le public n'est pas une propriété du défaut.** L'ADR-0027 séparait selon qui lit le message, et
cette séparation ne survit pas au contact de ce que les messages disent. Une déclaration de règle sans
`Id` publie un membre de catalogue qu'aucune suppression ne pourra jamais nommer ; une suppression qui
nomme ce membre de travers ne se résout à rien. Ni l'une ni l'autre ne fonctionne, et le modèle
précédent plaçait la première un niveau sous la seconde parce que la première est lue par un auteur de
paquet et la seconde par un consommateur de paquet. Il n'a par ailleurs pas de référent stable :
l'auteur d'un catalogue *est* un consommateur de la fondation, et le même projet est fréquemment les
deux.

**« Une build différente » était l'affirmation porteuse, et elle est fausse du défaut.** Que la build
de l'auteur soit séparée compterait si le défaut s'y arrêtait. Ce n'est pas le cas : le catalogue se
publie, les constantes sont livrées, et la défaillance est délivrée à tout l'aval sous une forme que
personne en aval ne peut voir. `DCAT0015` en est le cas le plus net — un paquet dont la raison d'être
entière est que ses consommateurs soient vérifiés, livrant une version qui ne vérifie personne — et
c'était celui maintenu le plus discret.

**La sous-détection n'est pas de l'incertitude.** Le second argument de l'ADR-0027, conservé pour
`DCAT0009`, confond un faux négatif avec un faux positif. Une forme que l'analyzer ne reconnaît pas
est une forme dont il ne dit rien ; cela ne rend pas moins certaines celles qu'il reconnaît.
`DCAT0009` signale une ligne que *tous* les outils de la chaîne écartent — l'auteur croit qu'un
avertissement est réduit au silence et il ne l'est pas — et l'existence d'une seconde forme que
personne n'a apprise à l'analyzer n'est pas une raison d'adoucir cela. Pris à l'envers, l'argument
interdit à tout diagnostic d'être une erreur tant que sa couverture n'est pas totale, ce qu'aucune
couverture n'est jamais.

**« C'est neuf » est une raison de surveiller une règle, pas de la classer.** L'écart de l'ADR-0039
portait sur le *calendrier* et non sur ce que dit `DCAT0014`, et il le disait — l'action de suivi
demande une révision une release plus tard. Cette release n'a pas eu lieu, et la raison pour laquelle
la question peut être tranchée maintenant est le troisième fait du contexte : rien n'est publié, donc
le coût contre lequel l'enregistrement protégeait — un consommateur existant rencontrant un mur
d'erreurs le jour de sa mise à jour — n'existe pas. La rencontre qui subsiste est la première build
après l'*adoption* d'un catalogue, que `DCAT0006` produit déjà et que le guide d'adoption échelonne
déjà avec une ligne d'`.editorconfig`. Ajouter `DCAT0014` à cette même première build coûte un
identifiant de plus sur la même ligne.

**Une justification fait partie du contrat, ce n'est pas un ornement.** L'ADR-0039 a établi qu'une
suppression sans raison détruit une information qu'aucun outil ne peut récupérer ensuite. Une exigence
dont le défaut est `Warning` est une exigence tenue par l'attention, ce qui est précisément l'argument
que l'ADR-0027 faisait valoir pour les trois identifiants qu'il promouvait. Les deux enregistrements
ont tiré des conclusions opposées de la même prémisse, à une release d'intervalle.

**Le niveau `Warning` garde un sens réel, et c'est ce qui donne un sens au niveau `Error`.** Ce qui
reste en `Warning` est exactement ce qui fonctionne et demeure fragile : une catégorie libre de
dériver de ses voisines (`DCAT0011`), un identifiant ancré à rien (`DCAT0012`), et un nom qui trompe
tout lecteur du site d'utilisation (`DCAT0013`). Aucun des trois ne signale une ligne qui échoue à
faire son travail, et `DCAT0013` n'a même pas de réparation qu'un outil puisse désigner — renommer le
type et réécrire l'identifiant sont deux changements entre lesquels seul l'auteur peut choisir. Un
modèle où presque tout serait une erreur serait l'ancien défaut au signe près, et porterait aussi peu
d'information.

**`Info` reste une exception unique et énoncée.** `DCAT0005` signale une divergence que son auteur ne
pouvait pas éviter, et ne la signale que pour que la frontière imposée un cran plus loin par
`DCAT0013` soit visible plutôt que silencieuse. C'est autre chose que « moins certain » ou « moins
urgent », raison pour laquelle c'est un identifiant et non un niveau vers lequel les choses glissent.

## Alternatives envisagées

### Conserver la séparation par public et ne promouvoir personne

Le statu quo, et il se défend par le coût : un auteur de catalogue écrivant ses règles à la main
rencontre six identifiants d'un coup le jour de sa mise à jour.

Rejeté parce que le coût est faible et unilatéral. Chaque catalogue publié par ce dépôt est généré et
ne peut pas déclencher ces identifiants ; la population qui les rencontre est celle qui déclare des
règles à la main, pour qui ces diagnostics sont la liste de contrôle d'un contrat auquel elle a
souscrit et qui n'est autrement documenté nulle part à la compilation. Et la séparation classe mal
`DCAT0015`, dont tout le sujet est un paquet qui manque à ses consommateurs : l'argument de public dit
« auteur », le défaut dit « tout l'aval ».

### Promouvoir les règles structurelles et laisser `DCAT0014` et `DCAT0015` une release en arrière

Cela honorerait littéralement le « revoir dans une release » de l'ADR-0039 et permettrait de recueillir
d'abord des retours d'adoption.

Rejeté parce que la release à laquelle cela renvoie est `1.0.0` elle-même. Attendre signifie livrer la
première version publiée de ces analyzers avec une sévérité que l'enregistrement décrit déjà comme
provisoire, puis la changer en `1.1.0` — ce qui, *cette fois*, est bien un changement dans la build
d'un inconnu, et la seule version de cette décision qui pourrait jamais l'être. Le moment économique
pour fixer un défaut est avant que quiconque en dépende, et ce moment est maintenant.

### Livrer tous les diagnostics en erreur

Simple, et cohérent avec « la garantie est une propriété de l'ensemble ».

Rejeté parce que cela efface la distinction que la sévérité est là pour porter. `DCAT0013` signale une
déclaration qui fonctionne, qui trompe, et qu'aucun outil ne peut réparer sans faire un choix
appartenant à son auteur ; faire échouer une build là-dessus rendrait le niveau vide de sens et
pousserait les équipes à faire taire la catégorie entière — le seul dénouement qui coûte plus cher que
n'importe quelle sévérité isolée.

### Exprimer le modèle comme une surface de configuration propriétaire

Un profil « strict » / « permissif » qu'un projet sélectionnerait, au lieu de défauts par identifiant.

Rejeté pour les raisons que l'ADR-0027 donnait déjà en faveur d'`.editorconfig` : les clés de sévérité
de Roslyn sont par identifiant et par chemin, c'est ce que toute équipe connaît déjà, et un second
format devrait réimplémenter le découpage par chemin pour être utile.

## Conséquences

### Positives

* Un nouvel identifiant se classe en posant une question sur ce qu'il signale, plutôt qu'en regardant
  le voisin à côté duquel il a été déclaré. Le modèle est énoncé une fois, dans `Descriptors.cs` et
  dans le guide.
* `DCAT0015` atteint la sévérité que son sujet mérite : un catalogue qui ne vérifierait silencieusement
  personne fait échouer la build qui l'aurait publié.
* La ligne unique d'`.editorconfig` déjà documentée pour `DCAT0006` couvre désormais toute la
  rencontre de la première build, parce que `DCAT0014` atterrit au même endroit au lieu d'un niveau
  plus discret que personne ne lit.

### Négatives

* Adopter un catalogue sur une base de code existante rencontre maintenant deux identifiants en
  sévérité erreur à la première build au lieu d'un. La ligne d'abaissement du guide d'adoption les
  nomme tous les deux.
* Un catalogue écrit à la main qui construisait avec six avertissements cesse de construire. La
  réparation est mécanique pour `DCAT0002`–`DCAT0004`, et le guide des diagnostics énonce le correctif
  de chacun.
* Un catalogue qui organise délibérément l'opt-in autrement doit maintenant le dire — via
  `DiagnosticCatalogAnalyzerOptIn`, ou une ligne d'`.editorconfig` — plutôt que de vivre avec un
  avertissement.
* **Promouvoir `DCAT0015` a d'abord obligé à resserrer son déclencheur.** MSBuild marque tout projet
  comme empaquetable par défaut : la classification qui le sous-tend lisait donc une application
  console ou une bibliothèque interne déclarant ses propres règles comme un catalogue publiant sans
  son opt-in. En avertissement, c'était du bruit ; en erreur, c'est une build qui tombe pour un paquet
  que personne ne publierait. Le verdict est désormais calculé pendant qu'un paquet est réellement
  produit, ce qui est à la fois là où le défaut existe et là où son message peut être suivi d'effet —
  et c'est une vraie réduction des moments où le diagnostic se voit, payée pour rendre la sévérité
  honnête.

### Risques

* **Les niveaux deviennent une étiquette plutôt qu'un test.** « Contrat obligatoire, incorrect, ou sans
  effet » est une phrase dans laquelle on peut lire n'importe quoi si l'on n'essaie pas.
  `DefaultSeverityTests` fige la table pour qu'un changement soit délibéré, et le guide énonce le
  niveau à côté de chaque identifiant, mais ni l'un ni l'autre ne peut forcer la question à être posée
  honnêtement.
* **Extinction de toute la catégorie.** Une première build qui échoue sur deux identifiants invite à
  `dotnet_analyzer_diagnostic.category-DiagnosticCatalog.severity = none`, ce qui éteint tout, y
  compris les vérifications que l'équipe voulait. Le guide de configuration distingue cette clé de la
  rampe par identifiant et d'`EnableDiagnosticCatalogAnalyzers`, qui sont trois comportements
  différents et étaient auparavant décrits comme si deux d'entre eux répondaient au même besoin.

## Actions de suivi

* Si des retours d'adoption montrent `DCAT0014` faisant échouer des builds sur des lignes dont la
  raison ne peut vraiment pas être écrite, revoir — avec les retours, pas avec cet argument.
* Quand un identifiant est ajouté, énoncer son niveau dans le commentaire de son descripteur et dans
  `DefaultSeverityTests` ; un nouvel identifiant sans niveau énoncé est la défaillance que cet
  enregistrement existe pour empêcher.

## Références

* [ADR-0027](0027-ship-the-use-site-diagnostics-as-errors.fr.md) — la séparation par public que cet
  enregistrement remplace, et la source des sévérités de `DCAT0002`–`DCAT0004`,
  `DCAT0011`–`DCAT0013` et `DCAT0009`.
* [ADR-0039](0039-require-a-justification-on-every-suppression.fr.md) — l'enregistrement qui a livré
  `DCAT0014` en avertissement et a demandé exactement cette révision.
* [ADR-0038](0038-stop-the-analyzers-at-the-project-that-references-a-catalogue.fr.md) — l'opt-in dont
  `DCAT0015` signale l'absence.
* [Les diagnostics `DCAT`](../guide/diagnostics.fr.md) — chaque identifiant, son niveau et sa clé
  `.editorconfig`.
* [Configuration](../guide/configuration.fr.md) — la rampe, l'interrupteur de catégorie et la propriété
  MSBuild, et pourquoi ce sont trois réponses à trois questions différentes.
