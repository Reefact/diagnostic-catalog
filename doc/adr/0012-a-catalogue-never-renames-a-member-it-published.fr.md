# ADR-0012 | Un catalogue ne renomme jamais un membre qu'il a publié

🌍 **Langues :**  
🇬🇧 [English](./0012-a-catalogue-never-renames-a-member-it-published.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Accepted
**Proposed:** 2026-07-30
**Accepted:** 2026-07-30
**Decision Makers:** Reefact

## Context

Un catalogue généré publie deux sortes de membres qu'un consommateur écrit à la
main : une classe de règle, référencée `SonarRule.S1144.Id`, et une constante de
catégorie, référencée `SonarCategory.MajorCodeSmell`. Les deux apparaissent dans
les sources du consommateur, à l'intérieur des arguments de
`SuppressMessageAttribute`.

Les membres de règles portent un identifiant choisi par l'éditeur ; leurs noms
suivent donc l'amont et ne peuvent pas dériver d'eux-mêmes. Les membres de
catégories, non : une catégorie d'éditeur est une chaîne libre — `"Major Code Smell"`,
`"StyleCop.CSharp.SpacingRules"` — et ce dépôt en dérive un identifiant C#. Des
chaînes distinctes peuvent dériver le même identifiant ; la dérivation comprend
donc une étape de désambiguïsation, et laquelle de deux catégories en collision
garde le nom simple est décidé par l'ordre dans lequel elles sont traitées.

Le contenu d'un catalogue est régénéré par un job nocturne sans surveillance dont
la sortie est une pull request à relire (ADR-0009, §14.3). Un relecteur lit ce
diff comme une déclaration sur l'amont : règles ajoutées, règles retirées,
catégories introduites. Un membre *renommé* n'est pas une forme que le diff
annonce.

Le §23.1 et ADR-0010 enregistrent déjà la garantie sœur pour les constantes de
règles : on n'en supprime jamais une, parce que les consommateurs incorporent les
valeurs de constantes à leur propre compilation et qu'un membre qui disparaît
casse leur recompilation. Aucune déclaration équivalente ne couvrait les noms des
constantes de catégories à côté.

Un membre renommé est une erreur de compilation pour chaque consommateur qui le
référençait. Contrairement à une valeur de catégorie fausse — que la plateforme
.NET ne lit jamais, et qui ne change donc aucun comportement — un renommage ne
peut pas être absorbé en silence : il arrête des builds.

## Decision

Un catalogue ne renomme jamais un membre qu'il a déjà publié ; le nom d'un
membre, une fois livré, est fixé pour la durée de vie de la version majeure de ce
catalogue.

## Rationale

La valeur qu'offre cette bibliothèque est qu'une référence à une règle est un
contrat que le compilateur vérifie. Un contrat dont les noms de membres peuvent
bouger sous un consommateur n'est pas un contrat ; c'est une version d'apparence
plus solide de la chaîne magique qu'il a remplacée, parce que le consommateur
croit désormais que le compilateur le protège.

La garantie doit être absolue plutôt qu'au mieux, pour la même raison qu'ADR-0010
rend la suppression impossible plutôt que rare. Le renommage arriverait par le
job nocturne — sans surveillance, la nuit, dans une pull request dont le diff est
lu comme un rapport sur l'amont plutôt que sur le nommage propre à ce dépôt. Un
relecteur qui ne cherche pas spécifiquement un renommage n'en verra pas. Toute
garantie qui dépend de ce relecteur n'est pas une garantie.

Dériver les noms de façon déterministe ne suffit pas en soi : le déterminisme
signifie que les mêmes entrées donnent les mêmes noms, et les entrées changent
chaque fois que l'amont ajoute une catégorie. La stabilité entre entrées
*différentes* est la propriété requise, et elle ne peut venir que du fait que la
sortie précédente soit une entrée de la suivante.

Le coût est accepté : un nom publié sous désambiguïsation garde sa forme
désambiguïsée même après la disparition de ce qui entrait en collision avec lui.
Cela laisse un catalogue porter à l'occasion un nom qui se lit moins bien qu'il
ne le faudrait. Le ranger signifierait renommer un membre publié — précisément ce
que cette décision interdit — la dette cosmétique est donc la bonne chose à payer.

La garantie est bornée par la version majeure, en accord avec la façon dont ce
dépôt traite déjà les identifiants : renommer ou retirer un membre référencé
symboliquement est un changement cassant (CLAUDE.md), qu'une release majeure peut
faire délibérément et visiblement. Ce que cette décision supprime, c'est le
renommage *accidentel*, pas le renommage réfléchi.

## Alternatives Considered

### Enregistrer la surface d'API publique de chaque catalogue et faire échouer la build quand elle change

`Microsoft.CodeAnalysis.PublicApiAnalyzers` protège déjà la surface propre de la
fondation, et son enregistrement inclut les valeurs de constantes ; un membre
renommé ou revalorisé serait donc en principe signalé.

Rejeté pour trois raisons. Le fichier source généré *est* l'enregistrement de
l'API publique — commité, diffable, et source réelle — un second fichier serait
donc une copie avec perte à maintenir en accord avec lui. La surface est assez
grande pour que l'enregistrement représente plusieurs milliers de lignes générées
à travers les catalogues. Et le job nocturne rend le mécanisme
auto-destructeur : si le job régénère l'enregistrement, rien n'est jamais
signalé ; s'il ne le fait pas, chaque pull request nocturne arrive en échec, avec
les entrées à réconcilier à la main.

De façon décisive, cela n'aurait pas empêché le renommage pour lequel le
mécanisme était envisagé. Un enregistrement régénéré accepte un nouveau nom aussi
volontiers qu'il a accepté l'ancien ; il rapporte que la surface a changé, après
avoir déjà consenti au changement.

### Accepter le renommage et le documenter comme changement cassant

Les renommages pourraient être permis et simplement rendus visibles — dans le
corps de la pull request nocturne, dans le changelog, dans les notes de version.

Rejeté parce que l'événement ne provient d'aucune décision de personne. Nul ne
choisit de renommer une constante de catégorie ; cela arrive parce qu'une
catégorie sans rapport est arrivée en amont et s'est triée d'une certaine façon.
Documenter un accident ne le rend pas intentionnel, et cela mettrait la build de
chaque consommateur à la merci du choix de ponctuation d'un éditeur.

### Dériver des noms qui ne peuvent pas entrer en collision

Une dérivation préservant davantage de la chaîne d'origine — ponctuation encodée
plutôt que retirée — rendrait les collisions impossibles et la question de
l'ordre sans objet.

Rejeté parce que cela échange un problème rare contre un problème permanent :
chaque constante de catégorie porterait un nom malcommode pour se prémunir d'une
collision qui, la plupart du temps, ne se produit pas. Cela renommerait en outre
chaque constante existante à l'introduction, ce qui est exactement le résultat
qu'on cherche à éliminer.

## Consequences

### Positive

* La référence d'un consommateur à une constante de catégorie continue de
  compiler à travers les mises à jour de catalogue au sein d'une version majeure.
* Le job nocturne ne peut pas introduire de renommage ; ses pull requests peuvent
  donc être lues comme ce qu'elles prétendent être : un rapport sur l'amont.
* La garantie pour les constantes de règles (ADR-0010) et celle pour les
  catégories à côté sont désormais la même promesse, plutôt que l'une écrite et
  l'autre supposée.

### Negative

* Un catalogue peut porter un nom désambiguïsé après la disparition de la
  collision qui l'a causé. Le nom est plus laid qu'il n'a besoin de l'être, à
  demeure.
* Générer un catalogue dépend désormais de la lecture de sa sortie précédente ;
  les deux moitiés du générateur sont donc plus étroitement couplées qu'avant.

### Risks

* La garantie ne tient que tant que la sortie précédente peut être relue. Si
  cette récupération venait à casser, chaque exécution ressemblerait à une
  première exécution et la protection cesserait de s'appliquer
  **silencieusement** — le mode de défaillance que ce dépôt existe pour éliminer.
  C'est couvert par des tests, et cette couverture est porteuse plutôt
  qu'accessoire.
* Un fichier de catalogue réécrit à la main, ou perdu puis régénéré à partir de
  rien, repart sans mémoire de ce qu'il publiait. La régénération est scriptée
  précisément pour que cela n'arrive pas, mais rien dans la build ne l'empêche.

## Follow-up Actions

* Aucune. La garantie est appliquée par le générateur et couverte par des tests ;
  il n'y a aucune vérification séparée à ajouter.

## References

* [ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.fr.md) — le
  contenu d'un catalogue est généré depuis les descripteurs d'analyseurs, et
  rafraîchi par un job sans surveillance.
* [ADR-0010](0010-carry-a-retired-rule-forward-as-obsolete.fr.md) — la garantie
  sœur pour les constantes de règles : n'en jamais supprimer une.
* Spécification §23.1 — une constante n'est jamais supprimée.
