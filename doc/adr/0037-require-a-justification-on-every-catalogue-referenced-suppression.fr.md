# ADR-0037 | Exiger une justification sur toute suppression référençant un catalogue

🌍 **Langues :**  
🇬🇧 [English](./0037-require-a-justification-on-every-catalogue-referenced-suppression.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Proposed
**Proposed:** 2026-08-06
**Decision Makers:** Reefact

## Contexte

Cette bibliothèque existe parce qu'une suppression est un contrat écrit dans des chaînes que rien ne
vérifie. Onze diagnostics couvrent aujourd'hui une moitié de ce contrat — quatre au site
d'utilisation, sept sur la déclaration — et tous répondent à la même question : **quel** diagnostic
une ligne fait taire. Une fois qu'ils sont satisfaits, le compilateur prend le relais, et une règle
renommée casse le build au lieu de ne plus rien supprimer en silence.

**L'autre moitié est `Justification`, et rien nulle part ne l'exige.**
`SuppressMessageAttribute` et `UnconditionalSuppressMessageAttribute` déclarent tous deux la
propriété ; tous deux la laissent facultative. Une suppression compile, se résout et fait taire son
avertissement sans elle, et aucun diagnostic de la plateforme ne dit un mot. Ce qui est perdu quand
elle manque ne se retrouve après coup pour personne : l'avertissement a disparu, il ne reste donc
rien à réexaminer, et la raison pour laquelle il était acceptable n'existait que dans la tête de qui
a écrit l'attribut. Un lecteur, six mois plus tard, ne distingue pas une suppression réfléchie d'une
suppression collée.

**La spécification exclut deux choses voisines, et aucune n'est celle-ci.** Le §5 range parmi les
non-objectifs « vérifier la qualité sémantique d'une justification » et « générer une justification
automatiquement » ; le §24 range « la validation intelligente des justifications » parmi ce que la
1.0 omet délibérément. Les trois portent sur le CONTENU d'une justification. Aucune ne porte sur sa
présence.

**La documentation est allée plus loin que la spécification.** Le guide des suppressions disait au
lecteur que cette bibliothèque « n'a aucune opinion sur le fait que supprimer cette règle *à cet
endroit* était une bonne idée. Ce jugement reste le vôtre, et c'est à cela que sert
`Justification` », et la suite d'usage portait une fixture — `DocumentedForms.NoJustification` — dont
le commentaire affirmait que « rien n'exige la présence de la propriété, et son absence n'est pas un
défaut que ces analyseurs connaissent ». C'était une lecture exacte des analyseurs tels qu'ils
étaient, écrite là où un lecteur la prend pour une décision.

**L'exigence existe dans l'écosystème, une fois.** `SA1404` de StyleCop, *Code analysis suppression
should have justification*, la couvre depuis des années, sur toute suppression y compris celles
écrites entièrement en littéraux. L'atteindre suppose de prendre `StyleCop.Analyzers` et ses
plusieurs centaines de règles de style, ce qui est une décision sur tout le style d'un codebase, pas
sur ses suppressions.

**Deux mesures bornent le coût.** La suite d'usage — 219 attributs de suppression écrits pour
ressembler à du code qu'un consommateur écrirait, et dont le build EST l'assertion que les analyseurs
restent muets dessus — a produit exactement **deux** signalements sous la nouvelle règle, tous deux
sur des fixtures qui existaient pour épingler l'ancien comportement. Et la vérification elle-même est
bon marché : elle lit un argument nommé sur un attribut que l'analyseur a déjà lié.

**Deux décisions existantes contraignent la forme.**
L'[ADR-0018](0018-a-code-fix-never-decides-what-only-the-author-can.fr.md) interdit à un correctif de
décider ce que seul l'auteur peut décider.
L'[ADR-0027](0027-ship-the-use-site-diagnostics-as-errors.fr.md) livre les diagnostics de site
d'utilisation en erreurs, sur l'argument que référencer un paquet catalogue est en soi la déclaration
d'intention — un argument tenu à propos de suppressions *fausses*, pas de suppressions correctes et
laconiques.

## Décision

Une suppression dont la catégorie ou l'identifiant référence une règle de catalogue doit porter une
`Justification` non vide, vérifiée par `DCAT0014` sur sa seule présence et jamais sur son contenu.

## Justification

**Le manque est celui-là même que la bibliothèque a été bâtie pour combler, un argument plus à
droite.** Chaque diagnostic de site d'utilisation existant rend le compilateur responsable de quelque
chose qu'un lecteur devait auparavant croire sur parole. `Justification` est le dernier argument de
l'attribut que rien ne vérifie, et le seul dont la perte est irrécupérable : un identifiant faux se
retrouve en relisant le code, une raison absente ne se retrouve pas du tout. La laisser dehors en
vérifiant tout autour est une frontière qui ne tient que parce que la spécification n'a jamais posé
la question.

**Présence et qualité sont deux questions, et une seule est hors de portée.** Juger ce que dit une
justification revient à juger la légitimité d'une suppression, ce que le §5 exclut pour de bonnes
raisons : un outil qui note de la prose se trompe dans les deux sens, il bénit les inepties bien
tournées et rejette une bonne raison dite brièvement. Que la propriété soit là ou non est un fait
structurel sur l'attribut — du même ordre que « l'identifiant se résout-il » — et il se tranche en
lisant la longueur d'une chaîne. Les non-objectifs sortent intacts de cette décision, et la
spécification le dit désormais là où elle les énumère.

**La restriction d'audience garde la règle adressée à ceux qui ont choisi.** Signaler une suppression
écrite entièrement en littéraux se déclencherait sur toutes les suppressions écrites à la main d'un
projet ayant référencé les analyseurs sans adopter de catalogue — l'argument de noyade que `DCAT0009`
tient déjà pour rester hors des littéraux, et pour lequel `DCAT0008` a été laissé en opt-in. La
restreindre aux suppressions qui référencent une règle rend aussi le relais net plutôt que
chevauchant : `DCAT0006` demande la migration, et celle-ci prend la ligne une fois la migration
faite.

**Elle est complémentaire de `SA1404`, pas un doublon.** Les deux diffèrent par leur coût et par leur
couverture. `SA1404` couvre toute suppression et coûte un paquet entier de règles de style ;
celle-ci couvre les suppressions qu'un projet a déjà déclarées comme références de catalogue et ne
coûte rien de plus que le paquet qu'il a déjà. Un codebase qui fait tourner les deux voit `SA1404`
d'abord sur les littéraux et celle-ci après la migration, soit deux règles d'accord plutôt que deux
règles qui se contredisent.

**Un avertissement plutôt qu'une erreur, en s'écartant délibérément du défaut de l'ADR-0027.**
L'argument de cet enregistrement est qu'un projet référençant un catalogue a décidé que ses
suppressions sont des références, et qu'une suppression qui n'en est *pas une* doit donc casser le
build. Cette règle-ci signale autre chose : une suppression qui est une référence, qui se résout
correctement, et qui est laconique. Casser tous ces builds le jour de la mise à jour du paquet
punirait les projets ayant adopté un catalogue avant l'existence de la règle, pour des lignes que
rien n'avait jamais interrogées. La sévérité est à une ligne d'`.editorconfig` pour qui la veut tout
de suite, ce qui est l'escalade que le seul fait de la signaler procure — le raisonnement même qui a
maintenu `DCAT0013` en avertissement.

**Aucun correctif, par l'ADR-0018 exactement.** La justification est la seule partie de l'attribut
qui ne se lit pas dans le code. Un correctif ne pourrait qu'insérer un marqueur, et la règle refuse
déjà le marqueur de la plateforme comme réponse.

## Alternatives envisagées

### Pointer vers `SA1404` et ne rien livrer

L'exigence existe déjà, implémentée et maintenue par StyleCop, et ajouter au build de chaque
consommateur une règle qui en double une existante a un coût réel.

Rejetée parce que l'atteindre coûte `StyleCop.Analyzers` en entier. Une équipe qui a adopté un
catalogue pour rendre ses suppressions vérifiables n'a rien dit sur son envie de plusieurs centaines
de règles de style, et lui répondre que la moitié manquante du contrat est disponible dans un autre
paquet revient à lui dire que la bibliothèque s'arrête un argument avant sa propre thèse. Les deux
cohabitent pour qui veut les deux, et c'est ce qui rend celle-ci bon marché plutôt que redondante.

### Signaler toute suppression, littéraux compris

C'est ce qu'attend un lecteur qui demande « quelque chose exige-t-il la justification ? », et c'est
ce que fait `SA1404`.

Rejetée sur l'argument de noyade que ce dépôt a déjà tenu deux fois — pour `DCAT0009`, qui reste hors
des littéraux, et pour `DCAT0008`, laissé en opt-in parce qu'un projet référençant des analyseurs
sans catalogue correspondant serait sinon submergé. Référencer les analyseurs ne doit pas transformer
chaque suppression préexistante écrite à la main en avertissement sur une propriété que personne ne
lui avait demandée. La couverture perdue est plus petite qu'il n'y paraît : `DCAT0006` signale les
littéraux d'abord, et celle-ci prend le relais à mesure qu'ils sont convertis.

### La livrer en erreur, avec les autres règles de site d'utilisation

L'argument de l'ADR-0027 est général, et une justification facultative en pratique est une
justification que la moitié du codebase n'écrira pas.

Rejetée parce que l'argument de cet enregistrement porte sur des suppressions fausses. Toute erreur
de site d'utilisation existante signale une ligne qui ne fait pas ce qu'elle a l'air de faire ;
celle-ci signale une ligne qui fait exactement ce qu'elle a l'air de faire et ne dit rien du
pourquoi. Les deux lectures d'« adopter un catalogue est une déclaration d'intention » ne survivent
pas à leur application à du code correct — le premier build après une mise à jour de paquet n'est pas
le moment d'en découvrir plusieurs centaines. La porte reste ouverte : dans une version, les formes
de faux positifs de la règle étant connues, la promouvoir est un changement de deux lignes et sa
propre décision.

### Juger la justification plutôt que seulement l'exiger

Une justification exigée mais vide invite au `"x"`, et une règle qui accepte `"x"` peut se lire comme
du théâtre.

Rejetée, et fermement. Le §5 l'exclut, le §24 exclut sa version intelligente, et les deux ont
raison : longueurs minimales et listes de mots interdits rejettent les bonnes raisons brèves et
acceptent les mauvaises bien tournées, et chaque projet finirait par configurer autour de la
vérification plutôt que par s'en servir. Ce qui subsiste est une exception étroite qui n'est pas un
jugement de contenu — le marqueur `<Pending>` de l'IDE, reconnu exactement, parce qu'il est le jeton
littéral d'un outil pour « pas encore écrit » plutôt qu'une opinion sur de la prose. Savoir si `"x"`
est une vraie raison est une question de revue de code, et c'est là qu'elle appartient.

## Conséquences

### Positives

* la seconde moitié du contrat d'une suppression est vérifiée, par le paquet même qui vérifie la
  première ;
* la vérification est la plus faible qui comble le trou, si bien que les non-objectifs sur le contenu
  restent intacts et restent honnêtes ;
* un codebase qui migre vers un catalogue est invité à donner la raison au moment où le code, et la
  personne qui a supprimé, sont encore devant celui qui convertit ;
* la règle ne demande rien de neuf au consommateur — pas de paquet, pas de configuration, pas
  d'attribut.

### Négatives

* un projet ayant adopté un catalogue avant cette règle voit de nouveaux avertissements sur du code
  correct, en nombre proportionnel au peu de justifications qu'il écrivait ;
* deux fixtures de la suite d'usage qui documentaient le comportement précédent ont dû changer, et la
  phrase du guide des suppressions sur l'absence d'opinion des analyseurs a dû être nuancée ;
* un diagnostic de plus sur une page qu'un lecteur doit déjà tenir en tête.

### Risques

* **`"x"` comme réponse.** Rien n'empêche un codebase de s'acquitter de la règle avec un mot.
  Accepté : l'alternative est de juger de la prose, et une revue attrape ce qu'une longueur ne peut
  pas.
* **L'exception du marqueur qui s'étend.** `<Pending>` est reconnu exactement aujourd'hui ; un futur
  contributeur y lisant l'autorisation d'ajouter `"TBD"`, `"n/a"` et consorts transformerait une
  vérification de marqueur en jugement de prose, une chaîne à la fois. La frontière est énoncée dans
  le descripteur, dans le guide et dans les tests, en ces termes, pour cette raison.
* **Pression à la promotion.** Un avertissement facile à satisfaire invite à être haussé en erreur
  avant que ses formes de faux positifs soient connues. Le tableau des sévérités et cet
  enregistrement disent ce qu'il faudrait d'abord établir.

## Actions de suivi

* réexaminer la sévérité dans une version, à l'aune de retours d'adoption réels plutôt que de cet
  argument ;
* si la validation de `Scope`/`Target` (§25.1) est livrée, revoir si les deux vérifications de site
  d'utilisation portant sur les *propriétés* de l'attribut doivent être décrites ensemble dans le
  guide plutôt que séparément.

## Références

* [ADR-0027](0027-ship-the-use-site-diagnostics-as-errors.fr.md) — le défaut de sévérité dont cet
  enregistrement s'écarte délibérément, et l'argument sur lequel il repose.
* [ADR-0018](0018-a-code-fix-never-decides-what-only-the-author-can.fr.md) — pourquoi aucun correctif
  n'est offert.
* [Les diagnostics `DCAT`](../guide/diagnostics.fr.md) — `DCAT0014` tel qu'un consommateur le
  rencontre.
* [Spécification §11.14](../specification.fr.md) — la condition de déclenchement, et le §5, où les
  non-objectifs sur le contenu disent maintenant ce que cette décision touche et ne touche pas.
