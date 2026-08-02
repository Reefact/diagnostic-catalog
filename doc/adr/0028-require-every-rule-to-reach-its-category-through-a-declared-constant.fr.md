# ADR-0028 | Exiger que chaque règle atteigne sa catégorie via une constante déclarée

🌍 **Langues :**  
🇬🇧 [English](./0028-require-every-rule-to-reach-its-category-through-a-declared-constant.en.md) | 🇫🇷 Français (ce fichier)

**Statut :** Proposé
**Proposé :** 2026-08-02
**Décideurs :** Reefact

## Contexte

Une règle satisfaisait quatre exigences : le marqueur, une classe statique non
générique, une `const string Id` publique et une `const string Category` publique. Rien
ne disait d'où venait la *valeur* de la catégorie. `Category = "Usage"` et
`Category = ContosoCategory.Usage` satisfaisaient l'un comme l'autre le contrat, et la
documentation le disait explicitement à cinq endroits — le résumé de l'attribut
lui-même, le guide du contrat, le guide d'écriture, le glossaire et la spécification
qualifiaient tous `[DiagnosticCategory]` d'optionnel.

Les deux formes sont indiscernables en aval. Une `const` initialisée depuis une autre
`const` est une constante de compilation : les deux se replient sur le même littéral
dans les métadonnées, les deux sont des arguments d'attribut valides, et les deux
suppriment exactement le même diagnostic. Rien dans la plateforme, dans l'assemblage
émis ou chez un consommateur qui réfléchit ne peut les distinguer. Le seul composant qui
le peut est un analyzer qui lit l'initialiseur.

Trois faits pèsent sur la question de savoir si cette différence mérite d'être signalée.

Un catalogue répète très peu de catégories distinctes sur un très grand nombre de
règles : le catalogue Sonar dépense 456 déclarations de règles sur 13 valeurs, StyleCop
193 sur 8. Chaque transcription est un endroit où l'une d'elles peut dériver, et une
catégorie qui a dérivé n'a aucun symptôme — Roslyn apparie une suppression sur son seul
identifiant (§3.2), si bien qu'une catégorie mal orthographiée ne change rien qu'un
build, un test ou un outil puisse observer.

Les quatre catalogues que ce dépôt génère déclarent déjà leurs catégories une seule
fois, dans un conteneur marqué. Tous les exemples du guide d'écriture le font aussi, dès
sa deuxième section. Ce que le contrat permettait et ce que ce dépôt pratiquait avaient
déjà divergé ; l'exemple minimal du README racine était le bord visible de cet écart, et
c'est là que la question a été posée.

Le marqueur est ce qui rend le conteneur lisible par l'outillage. Sans lui, un analyzer
ne peut pas distinguer une constante de catégorie de n'importe quelle autre constante
`string` d'un assemblage, si bien qu'un correctif remplaçant un littéral de catégorie
n'a rien à proposer à la place. Cette capacité existe aujourd'hui par catalogue : elle
fonctionne pour un catalogue qui a opté pour, et ne fait silencieusement rien pour un
autre.

`DiagnosticCatalog.Analyzers` n'a aucune version sur nuget.org. Son
`AnalyzerReleases.Shipped.md` est vide, donc aucun build de consommateur ne voit
actuellement le moindre diagnostic `DCAT`.

## Décision

**Le `Category` d'une règle doit se résoudre vers une `const string` déclarée dans une
classe marquée `[DiagnosticCategory]`**, ce qui devient la cinquième exigence du contrat
structurel et est signalé par `DCAT0011` en `Warning`.

## Raisonnement

L'exigence ne rend pas juste une catégorie fausse, et il ne faut pas la défendre comme
si c'était le cas. Une constante de catégorie est déclarée par la même main, dans le même
assemblage, au même moment que la règle qui la nomme ; il n'y a pas de référent
indépendant avec lequel la référence pourrait être en désaccord, si bien que
l'indirection déplace le point de vérité unique au lieu d'en créer un second pour le
vérifier. Être cohérent sur une valeur fausse n'est pas la même chose qu'avoir raison.

Ce que cela achète, c'est l'uniformité, et l'uniformité est ce qui est acheté
délibérément. Chaque catalogue a alors une seule forme : un lecteur qui passe de l'un à
l'autre voit la même, et l'outillage peut compter sur l'existence du conteneur plutôt que
l'espérer. Ce dernier point est le gain concret — le correctif qui propose une constante
nommée à la place d'un littéral de catégorie cesse d'être une capacité que certains
catalogues supportent par hasard pour devenir une capacité qui marche toujours. Une
capacité disponible uniquement là où un auteur a opté pour est une capacité sur laquelle
aucun consommateur ne peut compter.

Cela referme aussi l'écart entre ce que le contrat permettait et ce que ce dépôt faisait
déjà. Du code généré qui ne ressemble pas au code que la documentation dit d'écrire est
une invitation permanente à se demander lequel a raison, et la réponse était « les
deux », soit la réponse la moins utile disponible.

Le coût est d'une classe par catalogue, dans un fichier qui porte déjà des centaines de
lignes de règles. Le catalogue assez petit pour que cette classe soit une vraie charge —
une seule règle, une seule catégorie — n'est pas un catalogue que quiconque publie. Mise
en regard d'une exigence qui vaut pour toute la vie du catalogue, une classe écrite une
fois n'est pas un prix sérieux.

`Warning` plutôt qu'`Error` suit l'[ADR-0027](0027-ship-the-use-site-diagnostics-as-errors.fr.md)
plutôt que de s'en écarter : le public est celui qui écrit un catalogue, pas celui qui en
consomme un, et tout diagnostic de déclaration s'adresse à ce public. Il n'y a d'ailleurs
rien ici qui échoue. La règle compile, se replie correctement et supprime ce qu'elle
doit ; ce qui ne va pas est une propriété du catalogue, pas un défaut de la déclaration.
Le signaler comme une erreur revendiquerait une sévérité que les faits ne portent pas.

Le faire maintenant plutôt que plus tard est ce qui rend l'opération peu coûteuse. Le
paquet d'analyzers n'a jamais été publié, donc l'exigence atteint son premier
consommateur comme une partie du contrat plutôt que comme un changement de celui-ci. La
même exigence ajoutée après publication rendrait bruyant d'un coup le build de tous les
catalogues existants, pour une propriété qu'on ne leur avait jamais demandé d'avoir.

## Alternatives considérées

### Laisser le marqueur optionnel et le recommander dans les guides

Le statu quo, et l'option la moins chère : les guides recommandent déjà le conteneur dès
leur deuxième section, et le générateur l'émet déjà.

Rejetée parce qu'une recommandation est exactement ce qui échoue à livrer de
l'uniformité. Elle laisse la forme d'un catalogue dépendre de la page que son auteur a
lue, et elle laisse le correctif de remplacement de littéral incapable de compter sur la
présence d'un conteneur. L'écart entre la recommandation et le contrat est aussi ce qui a
produit la question à laquelle cet ADR répond.

### Signaler la divergence plutôt que la forme

Plutôt qu'exiger une forme, signaler deux règles d'un même assemblage dont les catégories
ne diffèrent que par la casse, l'espacement ou un quasi-doublon — le défaut réel que
prévient le fait de déclarer chaque catégorie une seule fois.

Rejetée comme décision, mais pas comme idée : elle attrape la dérive d'où qu'elle vienne,
n'exige aucun changement de contrat et ne se déclenche que là où la factorisation aurait
servi. Mais elle n'apporte rien à l'uniformité, qui est ce qui est acheté ici, et elle
reste muette sur un catalogue dont tous les littéraux se trouvent concorder aujourd'hui.
Elle reste souhaitable plus tard, à côté de cette exigence plutôt qu'à sa place.

### La livrer en erreur

Envisagée et d'abord retenue, au motif qu'une garantie laissée à l'attention n'est pas une
garantie.

Rejetée parce que rien n'est cassé dans une règle qui y échoue : elle compile, se replie
sur le bon littéral et supprime le bon diagnostic. Une erreur affirmerait une sévérité que
les faits ne portent pas, et elle contredirait le partage de l'ADR-0027 entre le build du
consommateur et celui de l'auteur sans raison que ce partage ne couvre déjà. La sévérité
reste configurable par projet dans `.editorconfig`, si bien qu'un auteur de catalogue qui
veut l'exigence appliquée durement peut la relever en une ligne.

### Proposer un correctif qui extrait le littéral

Reportée plutôt que rejetée. La réparation est une classe qui n'existe peut-être pas
encore, portant une constante que personne n'a nommée ; un correctif qui inventerait les
deux devinerait le vocabulaire du catalogue — les règles de nommage d'un conteneur généré
sont mécaniques, celles d'un conteneur écrit à la main ne le sont pas. À revoir une fois
la forme établie.

## Conséquences

### Positives

* Chaque catalogue a la même forme, quel qu'en soit l'auteur et qu'il soit généré ou non.
* L'outillage peut supposer qu'un conteneur marqué existe, si bien qu'un correctif
  remplaçant un littéral de catégorie a toujours une constante à proposer.
* La documentation perd un axe optionnel : une décision de moins pour un auteur de
  catalogue, et cinq pages qui n'ont plus à expliquer un choix.
* Les catalogues générés et la forme qu'enseignent les guides sont désormais la même.

### Négatives

* Tout catalogue écrit à la main doit déclarer un conteneur, y compris le plus petit.
* Un nouveau diagnostic de déclaration à documenter, traduire et tenir à jour.
* `DCAT0011` arrive sans correctif, là où les trois diagnostics de déclaration à côté de
  lui en portent tous un.

### Risques

* L'exigence achète de l'uniformité, pas de la correction, et les deux sont faciles à
  confondre. Un lecteur qui prend `DCAT0011` pour une protection contre une catégorie
  fausse a été induit en erreur par lui ; les pages qui le décrivent disent clairement que
  la valeur elle-même n'est vérifiée par rien. Si ce cadrage s'érode, l'exigence commence
  à être citée pour une garantie qu'elle ne fournit pas.
* C'est la première exigence du §8 qui ne peut pas être évaluée sur un symbole de
  métadonnées, parce qu'elle lit un initialiseur. `DCAT0010` couvrira donc quatre
  exigences sur cinq au travers d'une frontière d'assemblage plutôt que toutes, et
  l'asymétrie devra être gardée en tête au moment d'écrire ce diagnostic.

## Actions de suivi

* Étudier le contrôle de divergence intra-catalogue décrit plus haut, qui attrape une
  dérive que cette exigence n'attrape pas.
* Revoir la question d'un correctif pour `DCAT0011` une fois le conteneur devenu une
  forme attendue des auteurs.

## Références

* [ADR-0008](0008-express-a-rule-as-a-marked-static-class-of-constants.fr.md) — le
  contrat structurel auquel ceci ajoute une exigence.
* [ADR-0026](0026-reach-a-category-only-through-the-rule-that-carries-it.fr.md) —
  pourquoi un conteneur généré est `internal`, et pourquoi un consommateur ne nomme jamais
  une catégorie seule.
* [ADR-0027](0027-ship-the-use-site-diagnostics-as-errors.fr.md) — le partage de sévérité
  que ceci suit.
* [Le contrat de règle](../guide/rule-contract.fr.md) et
  [la spécification](../specification.fr.md), §7.7 et §8.5.
