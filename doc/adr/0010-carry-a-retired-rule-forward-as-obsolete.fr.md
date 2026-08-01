# ADR-0010 | Reporter une règle retirée en obsolète, ne jamais supprimer sa constante

🌍 **Langues :**  
🇬🇧 [English](./0010-carry-a-retired-rule-forward-as-obsolete.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Accepted
**Proposed:** 2026-07-30
**Accepted:** 2026-07-30
**Decision Makers:** Reefact

## Context

Un catalogue généré est produit à partir des descripteurs qu'un analyseur amont
déclare (ADR-0009) et est régénéré au fil des versions de cet analyseur. Une
régénération qui écrirait simplement ce que l'amont déclare aujourd'hui perdrait
toute règle que l'éditeur a cessé de déclarer.

Les éditeurs retirent bel et bien des règles.
`Microsoft.CodeAnalysis.NetAnalyzers` déclare `CA2109` et `CA2229` en version
6.0.0 et ne les déclare plus en 10.0.302.

Ce qu'un catalogue publie, ce sont des `const string`. C# incorpore la valeur
d'une constante dans l'assemblage référençant au moment de la compilation *du
consommateur* ; un consommateur ne dépend donc pas de la constante du catalogue à
l'exécution — ses assemblages déjà construits portent le littéral replié et ne
sont affectés par rien de ce que le catalogue fera ensuite. Il en dépend à chaque
recompilation.

Supprimer une constante publique casse donc la source, et la casse arrive à la
build suivante du consommateur, qui peut avoir lieu bien après sa montée de
version du paquet. Le message du compilateur nomme un membre qui n'existe pas.

Quand l'amont retire une règle, la suppression que le consommateur avait écrite
pour elle est devenue inerte : le diagnostic n'est plus signalé, donc rien n'est
supprimé. La suppression devrait être retirée. La question ouverte est de savoir
comment son auteur l'apprend.

Les catalogues versionnent et publient sur leurs propres trains de release,
indépendamment de la fondation (ADR-0002) ; un catalogue peut donc prendre une
version majeure sans rien faire bouger d'autre dans le dépôt.

## Decision

Une règle que l'analyseur amont a cessé de déclarer reste dans le catalogue et
est marquée obsolète, en nommant la version amont qui l'a retirée, et supprimer
une constante de règle est une version majeure de ce catalogue.

## Rationale

Les deux options interrompent le consommateur ; elles diffèrent par ce que
l'interruption dit. Une constante supprimée produit une erreur de compilation à
propos d'un membre qui n'existe pas, laquelle ne contient aucune trace de ce qui
s'est réellement passé — ni que la règle a été retirée en amont, ni dans quelle
version, ni que la bonne réponse est de supprimer la suppression plutôt que de
lui chercher un remplaçant. La seule piste du consommateur est le nom qu'il a
lui-même écrit. Une constante obsolète porte tout cela, sur la ligne exacte qui
doit changer, et désigne la réponse qu'il aurait de toute façon dû trouver. À coût
d'attention égal, l'une des deux s'explique.

Que la forme obsolète soit un avertissement plutôt qu'une erreur est la bonne
gravité, pas un adoucissement. Un retrait en amont est de l'entretien : la
suppression du consommateur est inerte, pas nuisible, et rien dans sa build n'est
cassé. Un échec ferme serait disproportionné à ce qui s'est passé, et pire, il
ferait des montées de version de catalogue une chose à repousser — l'inverse de
ce qu'un miroir des règles de quelqu'un d'autre attend de ses consommateurs.

La règle découle aussi de ce que la bibliothèque prétend qu'un identifiant de
règle est. Toute la proposition est qu'une référence est un contrat plutôt qu'une
chaîne ; un contrat retiré chaque fois qu'un tiers fait du ménage n'est pas un
contrat, et un consommateur qui a choisi les références symboliques pour gagner
en stabilité aurait acheté l'inverse. Le numéro de version d'un catalogue est la
seule chose qui l'informe sur la compatibilité, et une régénération qui
supprimerait silencieusement des membres publics ferait bouger ce numéro sur la
foi de la note de version de quelqu'un d'autre.

Le coût accepté est qu'un catalogue s'accumule. Au fil d'assez de versions amont,
une part de sa surface décrit des règles que personne ne peut déclencher, et ces
entrées apparaissent dans les listes de complétion qui font partie de ce que le
catalogue vend. C'est réel, et c'est le prix honnête de la promesse :
l'alternative est un artefact plus propre qui casse périodiquement ceux qui en
dépendent. Le coût est aussi borné — une entrée obsolète est une constante de
compilation et une ligne de documentation, et elle ne coûte rien du tout à un
consommateur qui ne la référence pas.

Réserver la suppression à une version majeure, plutôt que l'interdire purement et
simplement, garde une voie d'élagage sans affaiblir la promesse. La structure en
trains permet déjà à un catalogue de prendre une majeure sans entraîner celle de
la fondation ; l'option existe donc et son prix est correct : un consommateur qui
lit le numéro de version est prévenu, ce qui est exactement ce qui manquait à une
suppression silencieuse.

## Alternatives Considered

### Supprimer la règle et laisser le diff de régénération parler

Envisagé parce que cela garde le catalogue miroir exact de ce que l'amont déclare
aujourd'hui — une définition défendable de ce qu'est un miroir — et que c'est la
seule option qui empêche l'artefact de croître sans borne.

Rejeté parce que « miroir exact » décrit le mauvais artefact. Les consommateurs
référencent les membres du catalogue par leur nom, ce qui en fait une API et pas
seulement le reflet du paquet de quelqu'un d'autre. Le diff de régénération est
lu par le mainteneur de ce dépôt ; le consommateur n'en voit rien, et reçoit à la
place une erreur de compilation vide de sens.

### Garder la constante mais la laisser non marquée

Envisagé parce que c'est l'option la moins intrusive disponible : rien ne casse,
rien n'avertit, et chaque suppression existante continue de compiler exactement
comme avant.

Rejeté parce que c'est silencieux dans la direction qui compte. Le consommateur
garde une suppression qui ne supprime rien et n'en est jamais informé, et le
catalogue continue d'affirmer l'existence d'une règle que son éditeur a retirée —
un catalogue qui prétend être la réponse faisant autorité tout en en détenant
discrètement une périmée. Il échange un résultat faux et bruyant contre un
résultat faux et silencieux, qui est le mode de défaillance que ce dépôt exclut
ailleurs (ADR-0009).

### Déplacer les règles retirées dans un paquet legacy séparé

Envisagé parce que cela garde le catalogue vivant propre tout en préservant les
constantes pour qui les référence encore, et que cela fait de l'accumulation un
choix explicite de quelqu'un plutôt qu'une fatalité.

Rejeté parce que, de la position du consommateur, c'*est* une suppression : le
membre disparaît du paquet qu'il référence, et la remise en état consiste à
découvrir un second paquet et à l'ajouter. Cela résout le problème de propreté du
dépôt en déplaçant le coût sur les gens à qui la promesse a été faite.

### Escalader l'obsolescence en erreur au bout d'un certain temps

Envisagé parce que cela rend le nettoyage à terme obligatoire au lieu de
perpétuellement optionnel, et que cela éviterait aux entrées obsolètes de
s'attarder à jamais dans les listes de complétion.

Rejeté comme casse sans prix affiché. Cela ferait échouer des builds selon un
calendrier que ce dépôt a inventé, pour un changement fait par un tiers, sans
frontière de version qu'un consommateur puisse épingler ou raisonner — le même
problème de suppression silencieuse, simplement différé et muni d'un minuteur.

## Consequences

### Positive

* Monter la version d'un catalogue ne casse jamais la recompilation d'un
  consommateur.
* Un retrait en amont atteint le consommateur sous forme d'un message nommant la
  règle et la version qui l'a retirée, sur la ligne qui doit changer.
* Le numéro de version d'un catalogue continue de signifier ce que le
  versionnage sémantique dit qu'il signifie, parce que les suppressions sont
  versionnées plutôt qu'incidentes.

### Negative

* Les catalogues croissent de façon monotone ; dans un catalogue mûr, une partie
  de la surface décrit des règles qu'on ne peut plus déclencher.
* Ces entrées diluent la complétion, qui est l'une des raisons pour lesquelles on
  référence un catalogue.
* La génération n'est plus une fonction pure du paquet amont : produire un
  catalogue exige de savoir ce que ce catalogue publiait auparavant.

### Risks

* Une règle est retirée en amont puis restaurée, laissant une marque
  d'obsolescence permanente et fausse. Atténuation : la marque est dérivée à
  chaque régénération de ce que l'amont déclare à ce moment-là ; une restauration
  la retire donc sans intervention.
* Une règle est renommée plutôt que retirée, et le catalogue porte une ancienne
  entrée obsolète et une nouvelle entrée d'apparence sans rapport, rien ne les
  reliant. Atténuation : aucune automatique — la pull request de régénération
  liste ensemble les ajouts et les retraits, et c'est là qu'un humain peut
  reconnaître une paire.
* Un consommateur supprime globalement les avertissements d'obsolescence et ne
  nettoie jamais. Atténuation : aucune disponible ; un catalogue ne peut pas
  primer sur la configuration d'avertissements du consommateur, et la promesse
  s'arrête délibérément avant de le forcer.

## Follow-up Actions

* Énoncer la règle du jamais-supprimer dans la documentation destinée aux
  consommateurs de chaque catalogue : c'est une promesse faite aux consommateurs,
  pas une convention interne.
* Garder ajouts et retraits tous deux visibles dans la pull request de
  régénération, pour qu'un renommage puisse être reconnu comme tel.
* Décider, avant qu'un catalogue prenne sa première version majeure, si cette
  majeure élague réellement les entrées retirées ou si elles sont conservées
  indéfiniment.

## References

* [ADR-0002](0002-partition-releases-into-trains-by-commit-scope.fr.md) —
  pourquoi un catalogue peut prendre une version majeure seul.
* [ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.fr.md) — d'où
  vient le contenu d'un catalogue, et pourquoi le silence est la défaillance à
  éviter.
* [doc/specification.fr.md](../specification.fr.md) — §14.1, §23.1, et annexe
  A12.
* `eng/CatalogGen` — le générateur.
