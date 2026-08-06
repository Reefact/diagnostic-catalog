# ADR-0037 | Exiger une justification sur toute suppression

🌍 **Langues :**  
🇬🇧 [English](./0037-require-a-justification-on-every-suppression.en.md) | 🇫🇷 Français (ce fichier)

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

**`DCAT0006` ne couvre pas les littéraux, et ne peut pas être amené à le faire.** Il ne signale une
paire littérale que si une règle visible de la compilation lui correspond, délibérément : se
déclencher sur des littéraux sans correspondance signalerait toutes les suppressions écrites à la
main d'un codebase n'ayant adopté aucun catalogue, ce qui est aussi la raison pour laquelle
`DCAT0008` a été spécifié en opt-in. La conséquence, mesurée sur un projet référençant les analyseurs
et un catalogue de vendor, est une forme que rien ne signale :

| Suppression | Signalée par |
| --- | --- |
| une référence de catalogue, sans justification | cette décision, et rien avant elle |
| un littéral nommant une règle que le catalogue connaît | `DCAT0006` — la migration, pas la raison |
| un littéral nommant une règle qu'aucun catalogue ne connaît | **rien** |

La troisième ligne est celle qui décide de cet enregistrement. Une restriction aux références de
catalogue laisserait l'exigence absente précisément là où un codebase a le moins adopté, c'est-à-dire
là où les suppressions ont le moins de chances d'avoir été réfléchies.

**L'exigence existe dans l'écosystème, une fois.** `SA1404` de StyleCop, *Code analysis suppression
should have justification*, la couvre depuis des années, sur toute suppression. L'atteindre suppose
de prendre `StyleCop.Analyzers` et ses plusieurs centaines de règles de style, ce qui est une
décision sur tout le style d'un codebase, pas sur ses suppressions.

**Deux mesures bornent le coût.** La suite d'usage — 219 attributs de suppression écrits pour
ressembler à du code qu'un consommateur écrirait, dont environ dix-huit commencent par un littéral,
et dont le build EST l'assertion que les analyseurs restent muets dessus — a produit exactement
**deux** signalements, tous deux sur des fixtures qui existaient pour épingler le comportement
précédent, et **les mêmes deux que la règle couvre les seules références de catalogue ou toute
suppression**. L'élargir n'a rien coûté de mesurable sur ce corpus. Et la vérification elle-même est
bon marché : elle lit un argument nommé sur un attribut que l'analyseur a déjà lié.

**Deux décisions existantes contraignent la forme.**
L'[ADR-0018](0018-a-code-fix-never-decides-what-only-the-author-can.fr.md) interdit à un correctif de
décider ce que seul l'auteur peut décider.
L'[ADR-0027](0027-ship-the-use-site-diagnostics-as-errors.fr.md) livre les diagnostics de site
d'utilisation en erreurs, sur l'argument que référencer un paquet catalogue est en soi la déclaration
d'intention — un argument tenu à propos de suppressions *fausses*, pas de suppressions correctes et
laconiques.

## Décision

Toute suppression analysée par ce paquet doit porter une `Justification` non vide, que sa paire
référence une règle de catalogue ou soit entièrement écrite en littéraux, vérifiée par `DCAT0014` sur
sa seule présence et jamais sur son contenu.

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

**Toute suppression, parce que c'est la seule question qui ne dépend pas du catalogue.** Tous les
autres diagnostics d'ici doivent résoudre une règle pour avoir quoi que ce soit à dire ; celui-ci n'a
besoin que de l'attribut. Une suppression littérale fait taire un avertissement exactement comme une
référence et en dit exactement aussi peu sur le pourquoi ; une règle qui n'interrogerait que les
suppressions migrées interrogerait donc sur la base de quelque chose d'étranger à ce qu'elle vérifie.
L'argument de noyade qui tient `DCAT0009` et `DCAT0008` hors des littéraux ne se transporte pas : ces
deux-là ont besoin d'un index des règles connues pour dire quelque chose de vrai, et se trompent — ou
se taisent — là où le catalogue est absent. Celui-ci est exactement aussi vrai, et exactement aussi
actionnable, sur un littéral.

**Le coût de la couverture des littéraux a été mesuré plutôt qu'argumenté.** Le corpus écrit pour
ressembler à du code consommateur signale les deux mêmes sites sous les deux lectures de la règle.
Ce n'est pas la preuve qu'aucun codebase n'en rencontrera davantage, et les guides disent franchement
que l'adoption signale toutes les suppressions sans raison d'un coup — mais la forme redoutée, une
vague de signalements venant de code auquel le paquet n'avait rien à voir, n'est pas apparue là où
elle aurait été visible.

**Elle recouvre `SA1404`, et c'est la description honnête.** Les deux posent désormais la même
question, et un codebase qui fait tourner les deux verra les deux. Ce qui diffère est le prix
d'entrée : `SA1404` arrive avec plusieurs centaines de règles de style attachées, celle-ci arrive
avec le paquet qu'une équipe a déjà pris pour ses suppressions. Personne n'a rien à installer pour
l'obtenir, et un projet qui n'en veut qu'une fait taire l'autre en une ligne d'`.editorconfig`. Une
question posée deux fois coûte moins cher qu'une question que personne n'est en position de poser.

**Un avertissement plutôt qu'une erreur, en s'écartant délibérément du défaut de l'ADR-0027.**
L'argument de cet enregistrement est qu'un projet référençant un catalogue a décidé que ses
suppressions sont des références, et qu'une suppression qui n'en est *pas une* doit donc casser le
build. Cette règle-ci signale autre chose : une suppression qui se résout correctement et qui est
laconique. Elle signale de plus désormais dès l'adoption plutôt qu'après la migration, ce qui rend le
défaut « erreur » plus coûteux encore — un codebase référençant les analyseurs pour la première fois
rencontrerait un échec de build sur chacune de ses suppressions sans raison. L'avertissement garde
cette rencontre lisible, et la sévérité est à une ligne d'`.editorconfig` pour qui la veut tout de
suite. C'est le raisonnement même qui a maintenu `DCAT0013` en avertissement.

**Aucun correctif, par l'ADR-0018 exactement.** La justification est la seule partie de l'attribut
qui ne se lit pas dans le code. Un correctif ne pourrait qu'insérer un marqueur, et la règle refuse
déjà le marqueur de la plateforme comme réponse.

## Alternatives envisagées

### La restreindre aux suppressions qui référencent une règle de catalogue

C'était la première forme de la règle, et l'argument en sa faveur est réel : elle garde le diagnostic
adressé aux projets qui ont choisi le catalogue, elle reprend la ligne que `DCAT0009` trace déjà, et
elle fait que `DCAT0006` et celle-ci se passent proprement le relais au lieu de signaler deux fois la
même ligne.

Rejetée à cause de la troisième ligne du tableau du Contexte. Un littéral nommant une règle
qu'aucun catalogue référencé ne connaît n'est signalé par rien, et la restriction rend cela
définitif — l'exigence serait absente précisément là où un codebase a le moins adopté. Le relais
qu'elle achète est cosmétique : `DCAT0006` et `DCAT0014` signalent des défauts différents sur la même
ligne et survivent chacun au correctif de l'autre, si bien que les tenir séparés range la sortie d'un
build et laisse un trou dans tout projet qui ne migre jamais. La mesure a levé l'objection
restante — la règle élargie n'a rien coûté sur le corpus où le coût se serait vu.

### Pointer vers `SA1404` et ne rien livrer

L'exigence existe déjà, implémentée et maintenue par StyleCop, et ajouter au build de chaque
consommateur une règle qui pose la même question a un coût réel.

Rejetée parce que l'atteindre coûte `StyleCop.Analyzers` en entier. Une équipe qui a adopté un
catalogue pour rendre ses suppressions vérifiables n'a rien dit sur son envie de plusieurs centaines
de règles de style, et lui répondre que la moitié manquante du contrat est disponible dans un autre
paquet revient à lui dire que la bibliothèque s'arrête un argument avant sa propre thèse.

### La livrer en erreur, avec les autres règles de site d'utilisation

L'argument de l'ADR-0027 est général, et une justification facultative en pratique est une
justification que la moitié du codebase n'écrira pas.

Rejetée parce que l'argument de cet enregistrement porte sur des suppressions fausses. Toute erreur
de site d'utilisation existante signale une ligne qui ne fait pas ce qu'elle a l'air de faire ;
celle-ci signale une ligne qui fait exactement ce qu'elle a l'air de faire et ne dit rien du
pourquoi. Maintenant que la règle couvre toute suppression, un défaut « erreur » ferait de plus
échouer le premier build suivant la référence au paquet sur du code dont le paquet n'a jamais eu
d'opinion, ce qui est la pire introduction possible. La porte reste ouverte : dans une version, les
formes de faux positifs de la règle étant connues, la promouvoir est un changement de deux lignes et
sa propre décision.

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

* la seconde moitié du contrat d'une suppression est vérifiée, sur toute suppression, par le paquet
  qu'une équipe a déjà ;
* la seule forme que rien ne signalait — un littéral nommant une règle qu'aucun catalogue ne
  connaît — est couverte, et c'est celle dont un codebase ayant le moins adopté a le plus ;
* la vérification est la plus faible qui comble le trou, si bien que les non-objectifs sur le contenu
  restent intacts et restent honnêtes ;
* la règle ne demande rien de neuf au consommateur — pas de paquet, pas de configuration, pas
  d'attribut — et sa réponse ne dépend pas des catalogues référencés.

### Négatives

* référencer les analyseurs signale désormais toutes les suppressions sans raison d'un codebase d'un
  coup, et non les seules migrées ;
* une ligne en cours de migration est signalée deux fois, `DCAT0006` pour la paire et `DCAT0014` pour
  la raison, tant que les deux ne sont pas traitées ;
* elle pose la même question que `SA1404` pour un codebase qui fait tourner les deux ;
* deux fixtures de la suite d'usage qui documentaient le comportement précédent ont dû changer, et la
  phrase du guide des suppressions sur l'absence d'opinion des analyseurs a dû être nuancée.

### Risques

* **Bruit à l'adoption.** Le corpus dit que le coût est faible ; un codebase avec mille suppressions
  non documentées dira le contraire. La sévérité est un avertissement et le guide d'adoption nomme la
  ligne qui l'abaisse : c'est l'atténuation, pas un espoir.
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
* [Adopter un catalogue](../guide/adopting-a-catalogue.fr.md) — là où le coût du déclenchement large
  se rencontre, et la ligne qui l'abaisse le temps d'une migration.
* [Spécification §11.14](../specification.fr.md) — la condition de déclenchement, et le §5, où les
  non-objectifs sur le contenu disent maintenant ce que cette décision touche et ne touche pas.
