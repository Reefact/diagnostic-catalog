# ADR-0011 | Redistribuer les faits d'une règle uniquement, jamais la prose de l'éditeur

🌍 **Langues :**  
🇬🇧 [English](./0011-redistribute-rule-facts-only-never-the-vendors-prose.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Superseded by [ADR-0014](0014-ship-the-vendors-rule-title-as-a-catalogues-documentation.fr.md)
**Proposed:** 2026-07-30
**Accepted:** 2026-07-30
**Decision Makers:** Reefact

## Context

Les catalogues générés reflètent des analyseurs appartenant à d'autres personnes
— ceux de SonarSource, de Microsoft, du projet StyleCop.Analyzers. Chacun de ces
projets est livré sous sa propre licence ; les paquets produits ici sont livrés
sous celle de ce dépôt. Aucun des catalogues n'est affilié, approuvé ni supporté
par l'éditeur qu'il reflète, et chacun le dit.

Un `DiagnosticDescriptor` porte un identifiant, une catégorie, un titre, un
format de message, une description et un lien d'aide. L'identifiant et la
catégorie sont des faits sur le comportement du logiciel : ce que l'analyseur
signale et sous quelle catégorie il le signale. Le titre, le format de message et
la description sont des phrases que l'éditeur a écrites pour expliquer la règle ;
ils sont la substance de la documentation propre à cet éditeur. Un lien d'aide
est un pointeur vers l'endroit où l'éditeur publie cette documentation.

Un catalogue existe pour que les deux arguments obligatoires d'une suppression
deviennent des références vérifiées par le compilateur. Ces deux arguments sont
l'identifiant et la catégorie. Aucune part de cette fonction ne lit la prose.

Les trois paquets reflétés sont grands : respectivement 465, 318 et 193
descripteurs.

`SonarAnalyzer.CSharp` ne renseigne `HelpLinkUri` sur aucun de ses 465
descripteurs. `Microsoft.CodeAnalysis.NetAnalyzers` et `StyleCop.Analyzers` le
renseignent sur chacun des leurs. Les éditeurs publient leurs pages de règles à
des adresses qui suivent souvent un motif ; un lien d'aide pourrait donc en
principe être assemblé plutôt que lu.

La documentation de ce dépôt cite un titre de règle à deux endroits, pour montrer
la forme de la chaîne qu'insère le correctif de suppression intégré d'un IDE.

## Decision

Un catalogue tiers généré ne livre que les identifiants, catégories et liens
d'aide que ses descripteurs amont déclarent, et jamais les titres de règles, les
formats de message ou les descriptions de l'éditeur.

## Rationale

Un paquet est un canal de redistribution, et la décision porte sur ce que ce
dépôt est en droit d'y faire passer. Énoncer qu'un analyseur signale un
identifiant donné sous une catégorie donnée, c'est énoncer le comportement du
logiciel de quelqu'un d'autre ; cela ne reproduit rien de son travail, et aucune
licence n'est nécessaire pour le dire. Les titres et les descriptions, eux,
*sont* le travail — les phrases qui constituent la documentation de règles de
l'éditeur. En embarquer des centaines dans un paquet sous la licence propre de ce
dépôt ferait de ce paquet un dérivé du corpus de l'éditeur, et une permission que
ce dépôt ne peut pas accorder en son nom. Tracer la ligne entre un fait et un
texte d'auteur produit la seule version du miroir qui n'a besoin de la licence de
personne.

L'argument d'honnêteté court à côté de celui de licence et tiendrait même si les
licences étaient permissives. Un miroir non affilié portant le texte explicatif
de l'éditeur se lirait, pour un consommateur, comme la documentation de
l'éditeur. Il le traiterait comme faisant autorité, et il vieillirait contre les
pages de l'éditeur sans que rien ne le signale — l'éditeur ne peut pas corriger
une copie que ce dépôt a livrée. Pointer vers sa documentation garde le texte
faisant autorité là où son auteur le maintient, et garde la prétention du
catalogue modeste et défendable : voici l'identifiant et la catégorie de la
règle, et voici où son propriétaire l'explique.

Rien de ce à quoi le catalogue sert n'est perdu par la restriction. La prose ne
joue aucun rôle dans le fait de rendre une suppression vérifiée par le
compilateur ; son seul rôle serait le confort au moment de la lecture, et un lien
le fournit par un pointeur plutôt que par une copie — un pointeur à jour par
construction, ce qu'un instantané n'est jamais.

Citer un titre de règle dans une documentation pour illustrer un format est un
acte différent, et la distinction n'est pas une commodité. Une citation apparaît
dans une explication, en une poignée d'occurrences, faisant un travail dont
l'argument environnant a besoin et visiblement attribuable à son auteur ; le
lecteur voit ce qui est montré et à qui cela appartient. Embarquer le corpus dans
un artefact distribué fait du texte la charge utile plutôt que l'illustration, à
une échelle où ce qui est livré est tout simplement le catalogue de règles de
l'éditeur reformulé. Placer la règle sur ce qu'un *paquet* livre, plutôt que sur
la possibilité d'écrire un jour un titre, est ce qui la garde à la fois
applicable et raisonnable.

L'échelle fait partie de l'argument plutôt que d'y être accessoire. Un titre dans
un paragraphe illustrant un format est une citation, de quelque façon qu'on le
lise. Un titre et une description pour chaque descripteur des trois paquets
reflétés, émis mécaniquement et livrés en artefact, est une republication quel
que soit le nom qu'on lui donne.

## Alternatives Considered

### Livrer le descripteur entier, titres et descriptions compris

Envisagé parce que c'est ce que les descripteurs contiennent réellement, que cela
rendrait les infobulles de complétion véritablement informatives, et que cela
permettrait à un consommateur d'apprendre de quoi traite une règle sans quitter
son éditeur — une vraie amélioration du produit.

Rejeté parce que cela transforme chaque paquet en redistribution du corpus
d'auteur de l'éditeur sous une licence que ce dépôt ne peut pas accorder pour
lui, et met en circulation un instantané de sa documentation qu'il ne peut pas
corriger et qui ne porte aucune attribution à l'endroit où il est lu.

### Livrer les titres seulement, pas les descriptions

Envisagé parce qu'un titre est court, qu'il est la partie qui rend une infobulle
utile, et qu'il se lit davantage comme une étiquette que comme de la
documentation — le moins de prose pour l'essentiel du bénéfice.

Rejeté parce que la longueur n'est pas la distinction qui compte. Un titre reste
une phrase que l'éditeur a écrite, et plusieurs centaines d'entre eux restent son
catalogue. Une ligne tracée sur la brièveté devrait être défendue règle par règle
et bougerait sous la pression ; la ligne entre un fait sur le logiciel et un
texte écrit à son sujet, non.

### Synthétiser les liens d'aide depuis un motif d'URL par éditeur là où les descripteurs n'en déclarent pas

Envisagé parce que cela supprimerait l'asymétrie entre les catalogues et
donnerait à chaque règle un endroit où aller, et parce que les éditeurs dont les
descripteurs omettent le lien publient bel et bien leurs règles à des adresses
prévisibles.

Rejeté parce qu'un lien synthétisé est une valeur que ce dépôt a inventée et
présentée comme celle de l'éditeur. Si le motif est faux, ou change plus tard, le
catalogue livre des pointeurs cassés portant le nom de l'éditeur, et rien dans la
build d'un consommateur ne le signalerait jamais — la même inexactitude
silencieuse qu'ADR-0009 existe pour exclure, dans un autre champ du même
descripteur.

### Publier la prose séparément, dans son propre paquet ou dépôt

Envisagé parce que cela isolerait la question de licence du paquet de code et
permettrait à un consommateur d'opter délibérément pour la documentation plutôt
que de la recevoir par défaut.

Rejeté parce que la redistribution ne change pas de nature avec l'artefact qui la
porte : le texte est celui de l'éditeur d'où qu'il soit livré, et la question de
la permission est identique. Cela ajouterait en outre une seconde surface à
maintenir au pas de l'amont, pour un bénéfice qu'un lien d'aide fournit déjà.

## Consequences

### Positive

* Aucun paquet de ce dépôt ne redistribue le contenu d'auteur d'un autre projet ;
  la question de licence n'a donc pas à être rouverte éditeur par éditeur.
* Les consommateurs sont envoyés vers la page de l'éditeur, à jour par
  construction, plutôt que vers un instantané de celle-ci.
* Les catalogues restent petits, et un diff de régénération montre des règles qui
  bougent plutôt que des descriptions reformulées.

### Negative

* Les infobulles de complétion sur une constante de règle en disent moins
  qu'elles ne pourraient ; un consommateur qui veut savoir de quoi traite une
  règle suit le lien, ou l'identifiant.
* Un catalogue ne porte des liens d'aide que là où les descripteurs amont en
  fournissent. `SonarAnalyzer.CSharp` ne renseigne `HelpLinkUri` sur aucun de ses
  465 descripteurs ; le catalogue Sonar n'en porte donc aucun, tandis que les
  catalogues des analyseurs .NET et de StyleCop en portent un par règle.
  L'asymétrie est visible pour les consommateurs et ne peut pas être réparée sans
  synthétiser des liens, ce que cette décision exclut.
* La restriction doit être réénoncée catalogue par catalogue et ne peut pas être
  vérifiée par la build ; rien de mécanique ne distingue un fait d'une phrase.

### Risks

* Un mainteneur ajoute une constante de titre parce que la valeur est déjà sous
  la main et que l'amélioration d'infobulle est évidente. Atténuation : la
  restriction est enregistrée avec les règles du générateur et dans la
  documentation de chaque catalogue, et chaque fichier généré énonce dans son
  propre en-tête pourquoi la prose est absente.
* Un éditeur change ses adresses de documentation et les liens que ses
  descripteurs fournissaient se périment. Atténuation : la régénération porte ce
  que les descripteurs actuels déclarent ; un lien que ce dépôt n'a jamais écrit
  est un lien qu'il n'a jamais à maintenir.
* Un éditeur s'oppose au miroir lui-même plutôt qu'à la prose qu'il contient.
  Atténuation : chaque catalogue énonce clairement qu'il est non officiel et non
  affilié et reconnaît les marques auxquelles il se réfère ; au-delà, la question
  revient à un mainteneur, pas à un générateur.

## Follow-up Actions

* Énoncer dans la documentation destinée aux consommateurs de chaque catalogue ce
  que le paquet contient et où vivent les descriptions de règles de l'éditeur.
* Garder la restriction « faits uniquement » enregistrée avec le générateur, là
  où celui qui modifiera la génération la lira.
* Réexaminer la position si un éditeur publie des conditions explicites de
  redistribution de ses métadonnées de règles.

## References

* [ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.fr.md) —
  pourquoi une valeur qui n'a jamais été lue ne doit pas être inventée.
* [doc/specification.fr.md](../specification.fr.md) — §14.1, et annexes A9 et
  A11.
* `src/DiagnosticCatalog.Sonar/README.md` et ses homologues — ce que chaque
  catalogue dit à ses consommateurs.
* [LICENSE](../../LICENSE).
