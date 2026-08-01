# ADR-0020 | Un catalogue est généré pour C# uniquement

🌍 **Langues :**  
🇬🇧 [English](./0020-a-catalogue-is-generated-for-c-sharp-only.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Accepted
**Proposed:** 2026-07-31
**Accepted:** 2026-07-31
**Decision Makers:** Reefact

## Context

Le contenu d'un catalogue est dérivé en *construisant* chaque analyseur qu'un assemblage
déclare et en lisant les instances de `DiagnosticDescriptor` avec lesquelles il signale
(ADR-0009). La construction n'est pas accessoire à la méthode — elle *est* la méthode. Rien
d'autre dans la chaîne ne peut répondre à la question de la catégorie d'une règle, parce que
rien d'autre n'est ce avec quoi l'analyseur signale.

Construire un analyseur exige le Roslyn dont il dérive. Le worker de descripteurs porte
`Microsoft.CodeAnalysis.CSharp` et `Microsoft.CodeAnalysis.CSharp.Workspaces`. Il ne porte
pas `Microsoft.CodeAnalysis.VisualBasic`, et il n'existe aucun Roslyn pour F# — F# n'emploie
pas la plateforme.

Jusqu'à cette décision, `--language` acceptait `cs`, `vb` et `fs`, et l'`enum` du schéma de
manifeste listait les trois mêmes. La spécification (§25.4) énonçait qu'un catalogue Visual
Basic était donc « une entrée de manifeste plutôt que du code nouveau ». Mesuré contre
`Microsoft.CodeAnalysis.NetAnalyzers`, `--language vb` a résolu le paquet, l'a téléchargé, a
sélectionné deux assemblages, construit 265 analyseurs et lu 311 descripteurs — puis a
refusé, parce que trois types de `Microsoft.CodeAnalysis.VisualBasic.NetAnalyzers.dll` ne se
chargeaient pas.

Ce refus est le §14.3 fonctionnant correctement : un catalogue auquel il manque une règle est
indiscernable d'un catalogue dont l'éditeur l'a retirée, et l'émetteur publierait ces trois
règles en `[Obsolete]`, disant aux utilisateurs de cet éditeur quelque chose de faux sur leur
produit. Le défaut n'était pas le refus. C'était que l'outil ait annoncé l'exécution, et
tenu la promesse jusqu'au point même de la rompre, au prix d'un téléchargement.

Visual Basic est le seul langage auquel ceci serait plausiblement étendu, et sa trajectoire
est arrêtée. Microsoft a déclaré ne pas prévoir de faire évoluer Visual Basic en tant que
langage : aucune nouvelle fonctionnalité de langage, une approche de consommation seule
lorsque le runtime introduit quelque chose exigeant de la syntaxe, et aucune extension à de
nouvelles charges de travail. Le langage reste supporté et maintenu — il n'est pas abandonné
— mais il est clos. Sa population d'analyseurs est en conséquence petite et ne croît pas.

Séparément, et sans être affecté par cette décision : lire un *paquet* impose de reconnaître
quels dossiers appartiennent à quel langage, parce que les dispositions diffèrent.
`Microsoft.CodeAnalysis.NetAnalyzers` place l'essentiel de ses règles dans un assemblage
neutre en langage et seulement les règles spécifiques sous `cs/` et `vb/` ; une lecture C#
fonctionne donc en excluant les autres langages plutôt qu'en gardant son propre dossier.

## Decision

`dcat` génère des catalogues pour **C# uniquement**. `--language` et la clé `language` du
manifeste acceptent `cs` et rien d'autre, et une demande pour un autre langage est refusée
avant qu'aucun paquet ne soit résolu.

Cela repose sur deux jambes, et chacune suffirait à le tenir debout. L'outil **ne peut pas**
lire un autre langage, parce que le worker ne porte que le Roslyn C#. Et ce projet **ne
portera pas** celui de Visual Basic, parce que le langage est clos aux nouvelles
fonctionnalités et que sa population d'analyseurs est petite et ne croît pas.

## Rationale

La première jambe est une conséquence d'ADR-0009 plutôt qu'une politique. Parce que le
contenu vient d'analyseurs construits, l'ensemble des langages pour lesquels un catalogue
peut être généré est exactement l'ensemble dont le worker peut charger le Roslyn — pas
davantage, et non par choix. Cela serait vrai de n'importe quelle position sur Visual Basic,
y compris enthousiaste.

La seconde jambe est la position, et c'est un jugement plutôt qu'une conséquence. Microsoft a
clos Visual Basic aux nouvelles fonctionnalités de langage ; la population d'analyseurs
Visual Basic est petite et ne croîtra pas. Porter un second Roslyn dans chaque installation
d'un outil publié, et maintenir un second chemin de construction, est un coût continu contre
un retour décroissant. Ce projet le décline.

Enregistrer les deux importe, parce qu'elles échouent différemment. Si la seconde jambe était
la seule, un lecteur pourrait s'attendre à ce que l'option fonctionne avec un drapeau ou un
greffon ; si c'était la première, il pourrait s'attendre à ce que la restriction tombe le jour
où quelqu'un mesure le paquet. Ni l'un ni l'autre n'est vrai : le mécanisme explique pourquoi
l'outil ne le fait pas aujourd'hui, et le jugement explique pourquoi ce n'est pas prévu.

Un futur mainteneur qui demande « pourquoi C# seulement ? » devrait donc trouver les deux —
« le worker ne porte que le Roslyn C# » et « et nous n'en ajoutons pas un second, pour ces
raisons » — plutôt qu'un débat sur les langages qui méritent de l'outillage, ce qui n'est pas
le sujet.

Refuser d'emblée plutôt qu'à la fin découle du même endroit. Les refus de l'outil existent
pour être actionnables ; un refus délivré après qu'un paquet a été téléchargé et des
centaines de descripteurs lus est une promesse que l'outil a dépensé de l'effort à tenir
avant de la rompre. Un appelant qui a demandé Visual Basic n'est pas aidé de découvrir à la
fin qu'il ne pouvait pas l'avoir, et une chaîne distingue « cette invocation est fausse,
aucune reprise ne la corrige » de « l'exécution n'a pas pu se terminer » par le code de
sortie — ce qui ne fonctionne que si l'invocation fausse est reconnue comme fausse.

Refuser aux deux points d'entrée n'est pas de la ceinture et des bretelles. Une entrée de
manifeste atteint l'exécution sans passer par aucune analyse d'options ; ne valider que le
drapeau laisserait donc la même demande vraie en ligne de commande et fausse dans le fichier
qui fait la même chose — ce qui est exactement la forme de désaccord silencieux que ce dépôt
existe pour éliminer.

Les langages qu'une *disposition de paquet* est connue pour employer sont délibérément
laissés tels quels, et incluent toujours Visual Basic et F#. Connaître un langage et pouvoir
le lire sont deux faits différents. Une lecture C# doit reconnaître un dossier `vb/` pour
l'exclure ; dériver l'ensemble d'exclusion de l'ensemble lisible garderait tout, et les règles
Visual Basic seraient absorbées dans un catalogue C# — une défaillance sans symptôme dans la
sortie, qui est la catégorie de défaillance que ce dépôt existe pour empêcher.

## Alternatives Considered

### Livrer le Roslyn Visual Basic dans le worker

Envisagé parce que cela ferait dire à `--language vb` ce qu'il annonçait, et parce que le
mécanisme fonctionne déjà par ailleurs : la disposition de paquet est traitée, le filtre de
langage est correct, et les descripteurs se lisent jusqu'au point de construction.

Rejeté, et pas seulement différé. `dcat` est un outil publié dont un consommateur paie la
taille à l'installation ; ADR-0019 a accepté une croissance de 6,4 Mo à 7,7 Mo, mais pour une
capacité dont chaque utilisateur a besoin. Celle-ci serait payée par chaque utilisateur pour
servir une population petite et, le langage étant clos aux nouvelles fonctionnalités,
appelée à ne pas croître. Un second chemin de construction devrait en outre être maintenu en
état aussi longtemps que l'outil existe, contre un amont qui explicitement ne bouge pas.

L'échange n'est pas assez serré pour être laissé ouvert. Ce qui le rouvrirait, c'est un
changement de prémisse — Visual Basic reprenant son développement, ou une demande concrète que
ce projet accepte de servir — et non une mesure du paquet, qui ne ferait que chiffrer un coût
déjà jugé non rentable.

### Laisser l'option acceptée et laisser l'exécution refuser

Envisagé parce que le refus a déjà lieu, qu'il est correct, et qu'il s'explique — rien n'est
silencieusement faux aujourd'hui.

Rejeté parce que « correct à la fin » n'est pas la même chose qu'honnête au départ. Le texte
d'aide de l'option, et l'`enum` du schéma, sont lus par des gens qui décident de ce qu'ils
vont tenter ; tous deux énonçaient une capacité que l'outil n'avait pas. Un éditeur qui
complète `"language": "vb"` depuis un schéma qui le liste est la dérive que ce dépôt refuse,
à un cran de distance : l'artefact qui existe pour être vérifié était lui-même faux.

### Retirer entièrement `--language` et la clé de manifeste

Envisagé parce qu'une option à valeur unique est un bouton qui ne tourne nulle part, et que
`dcat` n'a aucune version publiée ; le retirer maintenant ne coûterait donc rien.

Rejeté parce que la clé est l'endroit où cette décision est exprimée à un utilisateur, et
parce que le filtrage qu'elle nomme est porteur qu'il soit sélectionnable ou non. La retirer
masquerait le fait qu'un langage a été choisi, et il faudrait la réintroduire — en changement
cassant, d'ici là — le jour où le worker portera un second Roslyn.

## Consequences

### Positive

* Un langage que l'outil ne peut pas lire est refusé comme erreur d'usage, avant qu'un
  paquet soit résolu, avec la raison et le remède.
* La ligne de commande et le manifeste répondent à la même demande de la même façon.
* Le schéma le signale dans un éditeur, avant même que l'outil soit lancé.
* La spécification ne prétend plus qu'un catalogue Visual Basic est une entrée de manifeste.

### Negative

* Les analyseurs Visual Basic ne peuvent pas être catalogués par cet outil, et ce n'est pas
  une lacune en attente de travaux. Pour un analyseur VB de règles maison, la méthode
  enregistrée dans ADR-0009 est indisponible ici, et aucun contournement ne l'atteint — la
  réponse honnête à un tel utilisateur est que cet outil n'est pas pour lui.
* `--language` accepte une seule valeur, ce qui se lit comme un bouton qui ne tourne nulle
  part. Elle est conservée parce que la clé est l'endroit où cette décision est exprimée à un
  utilisateur, et parce que le filtrage qu'elle nomme est porteur qu'il soit sélectionnable
  ou non.

### Risks

* Énoncée en jugement, la position peut se lire comme un dédain envers Visual Basic. Ce n'en
  est pas un : le langage est supporté et maintenu, et rien ici ne dit le contraire. Ce qui
  est dit, c'est que la petite population d'analyseurs d'un langage clos ne justifie pas un
  second Roslyn dans chaque installation de cet outil.
* Les deux jambes peuvent être prises pour une seule, laissant un lecteur croire que la
  restriction tombe dès que quelqu'un empaquette la dépendance. `CatalogLanguages` énonce le
  mécanisme sur place et renvoie ici pour le jugement ; les deux sont donc atteignables
  depuis le code.

## Follow-up Actions

* Aucune. C'est une position arrêtée, pas une tâche différée. La rouvrir exige un changement
  de prémisse — Visual Basic reprenant son développement, ou une demande que ce projet décide
  de servir — plutôt qu'une mesure.

## References

* [ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.fr.md) — le contenu vient
  d'analyseurs construits, et c'est pourquoi le Roslyn du worker décide de l'ensemble des
  langages.
* [ADR-0010](0010-carry-a-retired-rule-forward-as-obsolete.fr.md) — pourquoi une règle
  manquante à une lecture est publiée comme retirée, et donc pourquoi une lecture incomplète
  doit refuser.
* [ADR-0017](0017-publish-the-generator-as-a-cli-on-its-own-release-train.fr.md) — l'outil
  dont la première alternative pèse la taille de paquet.
* [ADR-0019](0019-resolve-packages-through-the-users-own-nuget-configuration.fr.md) — le
  précédent d'acceptation d'une croissance de paquet quand la capacité sert chaque
  utilisateur.
* `doc/specification.fr.md` §14.3 et §25.4 — le refus sur lequel ceci s'appuie, et
  l'affirmation qu'il corrige.
