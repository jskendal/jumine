package game

// -----------------------------------------------------------------------------
// 🎯 ENUMS
// TRES TRES IMPORTANT : L'ordre est exactement le meme que ton C#.
// Les valeurs numeriques sont identiques bit pour bit.
// Si tu ajoute un effet plus tard tu l'ajoute A LA FIN, JAMAIS au milieu.
// -----------------------------------------------------------------------------

type EffectType int

const (
	EffectNeutral EffectType = iota
	EffectHealthPotion
	EffectDamageBomb
	EffectPoison
	EffectMissile
	EffectArmor
	EffectFreeze
	EffectLaser
	EffectSpray
	EffectCollisionDuel
)

type Direction int

const (
	DirectionNone Direction = iota
	DirectionUp
	DirectionDown
	DirectionUpAndDown
	DirectionLeft
	DirectionRight
	DirectionLeftAndRight
	DirectionAll
)

// -----------------------------------------------------------------------------
// 🎯 STRUCTURES
// Exactement champ pour champ, ordre pour ordre, ton code C#
// -----------------------------------------------------------------------------

// Action envoyé par un joueur pour un tour
type PlayerAction struct {
	PlayerID  int
	TargetRow int
	TargetCol int
}

// Contenu d'une cellule de la grille
type CellEffect struct {
	Type     EffectType
	Value    int
	Duration float32
	IsWeapon bool
}

// Etat complet d'un joueur
type PlayerState struct {
	ID                   int
	Health               int
	MaxHealth            int
	Row                  int
	Col                  int
	IsAlive              bool
	PoisonTurnsRemaining int
	ArmorTurnsRemaining  int
	FreezeTurnsRemaining int
	IsFrozen             int
	StartPoison          bool
}

type CollisionDuel struct {
	Row       int
	Col       int
	PlayerIDs []int
}

type DuelResult struct {
	WinnerId    int
	LoserId     int
	LoserNewPos Position
}

type Position struct {
	Row int
	Col int
}

type EffectHitInfo struct {
	PlayerId  int
	NewHealth int
}

type EffectWeight struct {
	Type     EffectType
	Chance   float32
	Value    int
	Duration float32
	IsWeapon bool
}

// -----------------------------------------------------------------------------
// 🎯 La seule nouvelle structure
// Celle ci remplace tes delegates C#. C'est exactement ce que tu as deja
// dans ta queue d'effet dans GameManager.
// -----------------------------------------------------------------------------
type GameEvent struct {
	Type            EffectType
	Rank            int
	PlayerId        int
	LauncherId      int
	Row             int
	NewHealth       int
	Hits            []EffectHitInfo
	WeaponDirection Direction
	Participants    []int
}

// -----------------------------------------------------------------------------
// 🎯 GameState
// -----------------------------------------------------------------------------
type GameState struct {
	CurrentTurn          int
	Rows                 int
	Cols                 int
	HasDoneFirstTurn     bool
	Grid                 [][]CellEffect
	Players              []PlayerState
	FutureRow            []CellEffect
	CurrentDuels         []CollisionDuel
	PlayerFinalPositions map[int]Position
}