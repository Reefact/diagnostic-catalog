# Les alternatives

🌍 **Langues :**  
🇬🇧 [English](./alternatives.en.md) | 🇫🇷 Français (ce fichier)

Pour quiconque compare avant d'adopter. Cinq autres façons de résoudre le même problème, chacune avec
ce qu'elle achète réellement — y compris ne rien faire, qui est une réponse légitime.

Le problème, redit : `[SuppressMessage("Major Code Smell", "S1144")]` prend deux chaînes que rien ne
valide, et elles échouent différemment — un identifiant faux finit par remonter, une catégorie fausse
jamais. [Pourquoi les chaînes magiques échouent](the-problem.fr.md) en est la version longue.

## En un coup d'œil

| Approche | Identifiant faux attrapé | Catégorie fausse attrapée | Le renommage suit | Trouver les références | Le retrait remonte |
| --- | --- | --- | --- | --- | --- |
| Littéraux, tels qu'écrits aujourd'hui | non | non | non | recherche textuelle | non |
| Un fichier de constantes que vous maintenez | à la compilation | non — la valeur reste la vôtre | oui | oui | non |
| `GlobalSuppressions.cs` | non | non | non | recherche textuelle | non |
| `#pragma warning disable` | non | s. o. — ne prend aucune catégorie | non | recherche textuelle | non |
| Un grep avant chaque montée de version | manuellement | non | non | s. o. | manuellement |
| **Un catalogue généré** | à la compilation | **à la génération, depuis le descripteur** | oui | oui | `CS0618`, en nommant la version |

La colonne qui les sépare est la deuxième, et c'est la colonne qu'aucune approche de cette liste, la
dernière exceptée, ne peut remplir. Une catégorie est une chaîne que seul l'éditeur publie et que
rien dans la plateforme ne lit ; la seule façon d'avoir raison à son sujet est de la lire dans la
chose qui la déclare.

## Un fichier de constantes que vous maintenez

Le geste évident, et un bon geste dans les limites du sien :

```csharp
internal static class Rules
{
    public const string S1144Id = "S1144";
    public const string S1144Category = "Major Code Smell";
}
```

**Ce que cela achète.** Tout ce que le compilateur peut vous donner : une faute de frappe donne
`CS0117`, le renommage fonctionne, *Rechercher toutes les références* fonctionne. Si votre base de
code a trente suppressions sur cinq règles, c'est réellement suffisant, et vous devriez sans doute
l'écrire plutôt que prendre une dépendance.

**Où cela s'arrête.** Les valeurs restent les vôtres. `"Major Code Smell"` a été tapé par quelqu'un,
depuis une source qui était un instantané — un billet de blog, le *Supprimer → Dans la source* d'un
IDE, un autre fichier. Être cohérent sur une valeur fausse n'est pas la même chose qu'avoir raison :
c'est une valeur fausse à un endroit au lieu de quarante, ce qui est mieux et n'est pas identique.

Cela ne sait pas non plus quand une règle est retirée. L'amont abandonne `S1144`, votre constante
reste, votre suppression continue de compiler et cesse de signifier quoi que ce soit.

**Où passe la ligne.** À peu près : quand vous ne pouvez plus dire, de mémoire, d'où vient chaque
valeur de catégorie. Un catalogue généré est ce fichier avec les valeurs lues dans le
`DiagnosticDescriptor` de l'analyseur lui-même et régénérées quand l'éditeur bouge — c'est
[ADR-0009](../adr/0009-generate-catalog-content-from-analyzer-descriptors.md), et la raison d'être de
`dcat`.

## `GlobalSuppressions.cs`

Sortir les suppressions du code pour les mettre dans un fichier :

```csharp
[assembly: SuppressMessage("Major Code Smell", "S1144", Scope = "member", Target = "~M:Contoso.Orders.Rebuild")]
```

**Ce que cela achète.** Elles sont toutes au même endroit : on peut les lire, les compter et les
relire comme un ensemble — ce qui est un vrai bénéfice, et orthogonal à tout le reste de cette page.

**Où cela s'arrête.** Cela change *où* vivent les chaînes, pas *ce qu'elles sont*. Les deux arguments
sont aussi peu vérifiés qu'avant, et une troisième chaîne les rejoint : `Target`, un identifiant de
commentaire de documentation qui ne survivra pas non plus à un renommage.

**Les deux se composent.** Un `GlobalSuppressions.cs` écrit contre des constantes de catalogue est
strictement meilleur qu'un écrit contre des littéraux, et cette bibliothèque n'a aucune opinion sur
le fichier où vivent vos suppressions.

## `#pragma warning disable`

```csharp
#pragma warning disable S1144
```

**Ce que cela achète.** La brièveté, et la portée : cela fonctionne sur des instructions et des
régions, là où un attribut a besoin d'une déclaration à laquelle s'accrocher.

**Où cela s'arrête.** La directive prend un jeton identifiant nu, pas une expression : il n'existe
aucune position où une constante pourrait être substituée. C'est **définitivement hors d'atteinte** —
un fait de grammaire, pas une fonctionnalité manquante. Elle ne prend d'ailleurs aucune catégorie, si
bien que la moitié du problème de cette page ne s'y applique pas.

Si votre base de code supprime surtout de cette façon, voyez
[quand ne pas s'en servir](when-not-to-use.fr.md#vous-supprimez-avec-pragma-pas-avec-des-attributs).

## Un grep avant chaque montée de version

La réponse par la discipline : avant de monter un paquet d'analyseur, chercher dans la base de code
chaque identifiant que vous supprimez et le confronter aux notes de version.

**Ce que cela achète.** Les retraits, si les notes de version les listent et si quelqu'un le fait
vraiment. Sur une petite base de code avec un mainteneur soigneux, cela marche.

**Où cela s'arrête.** C'est manuel, donc c'est fait quand quelqu'un y pense, ce qui n'est pas la
montée de version où cela compte. Cela n'attrape rien sur les catégories — les notes de version ne
listent pas une recatégorisation comme un changement cassant, parce que pour l'éditeur ce n'en est
pas un. Et cela grandit avec le nombre de dépôts, ce qui est le point où les équipes renoncent.

La version mécanisée de tout ceci est `dcat validate`, qui calcule le catalogue que la version amont
actuelle produirait et le compare à ce que vous avez — en sortant `2` quand ils diffèrent, et `1`
quand il n'a pas pu conclure, exprès, pour qu'une panne de flux ne soit jamais rapportée comme un
contrat qui a dérivé.

## Ne rien faire

Cela mérite d'être listé, parce que c'est la bonne réponse plus souvent que la documentation d'une
bibliothèque ne l'admet d'ordinaire.

**Ce que cela achète.** Aucune dépendance, aucun coût d'adoption, aucune migration, rien de nouveau à
apprendre pour un relecteur.

**Où cela s'arrête.** Exactement là où le nombre de suppressions fait qu'une seule d'entre elles
silencieusement fausse devient un coût que vous paieriez. Ce seuil est un jugement, et
[quand ne pas s'en servir](when-not-to-use.fr.md) est écrite pour vous aider à le porter contre
vous-même plutôt que pour la bibliothèque.

## Ce à quoi cette bibliothèque ne se compare pas

Deux choses sont parfois proposées comme alternatives et résolvent d'autres problèmes :

* **Un analyseur qui valide les chaînes de suppression.** Cette bibliothèque en livre un — `DCAT0006`
  et compagnie — et c'est délibérément la plus petite moitié. Une vérification sur une chaîne ne peut
  juger que les chaînes qu'elle reconnaît : `[SuppressMessage("Usage", "S1144")]` n'est signalé par
  rien, car ce peut être une mauvaise catégorie ou une règle d'un analyseur que vous n'avez pas
  catalogué, et rien ne peut le dire. C'est la constante qui lève l'ambiguïté, pas la vérification.
* **Un outil qui retire les suppressions inutiles.** `IDE0079` le fait déjà, et il répond à une autre
  question — « cette suppression est-elle encore nécessaire ? » plutôt que « cette suppression nomme-
  t-elle ce qu'elle prétend ? ». Utilisez les deux.

## Où aller ensuite

* [**Écrire des suppressions que le compilateur vérifie**](writing-suppressions.fr.md) — si la
  réponse est oui, c'est le guide pratique.
* [**Quand ne pas s'en servir**](when-not-to-use.fr.md) — si la réponse est pas encore.

---

<div align="center">
<a href="./when-not-to-use.fr.md">← Quand ne pas s'en servir</a> · <a href="./README.fr.md">↑ Table des matières</a> · <a href="./writing-suppressions.fr.md">Écrire des suppressions que le compilateur vérifie →</a>
</div>
