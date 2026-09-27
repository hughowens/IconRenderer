using System.Reflection;
using System.Text.RegularExpressions;

namespace IconRenderer.Browser;

internal sealed record IconPreset(string Name, IReadOnlyList<string> IconNames);

internal static class IconPresetCatalog
{
    private sealed record Definition(string Name, string Terms);

    private static readonly Definition[] Definitions =
    [
        new("Arcane & Magic", "magic,magical,spell,wizard,witch,wand,rune,alchemy,occult,sorcery,potion,oracle,tarot"),
        new("Blades & Melee", "sword,blade,dagger,knife,katana,rapier,saber,sabre,cutlass,scimitar,cleaver,broadsword,mace,axe"),
        new("Bows & Projectiles", "arrow,bow,crossbow,bolt,quiver,dart,shuriken,slingshot,spear,javelin,harpoon"),
        new("Firearms & Explosives", "gun,rifle,pistol,shotgun,firearm,cannon,bomb,grenade,bullet,mine,dynamite,explosion,explosive,ammo"),
        new("Armor & Shields", "armor,armour,shield,helmet,helm,gauntlet,greave,cuirass,pauldron,breastplate,chainmail"),
        new("Fantasy Creatures", "dragon,griffin,phoenix,unicorn,hydra,kraken,chimera,basilisk,wyvern,pegasus,sphinx,giant,elf,dwarf,orc,goblin,troll,fairy"),
        new("Monsters & Undead", "skull,zombie,skeleton,ghost,vampire,demon,devil,monster,ghoul,lich,undead,mummy,wraith,corpse,necromancer,haunted"),
        new("Anatomy & Body", "body,hand,arm,leg,foot,eye,heart,brain,bone,teeth,tooth,lung,spine,blood,skull,organ,face,head"),
        new("Healing & Medicine", "health,medical,medicine,healing,bandage,hospital,pill,syringe,plaster,stethoscope,thermometer,drip,firstaid,crutch"),
        new("Mammals", "animal,bear,cat,dog,wolf,fox,horse,deer,rabbit,hare,mouse,rat,lion,tiger,panther,elephant,monkey,ape,camel,sheep,goat,cow,bull,pig,boar,squirrel,beaver,otter,badger,whale,dolphin,seal,bat"),
        new("Birds", "bird,eagle,hawk,owl,raven,crow,duck,goose,chicken,rooster,parrot,pigeon,falcon,swan,penguin,feather,nest,beak,wing,hummingbird"),
        new("Fish & Sea Life", "fish,shark,whale,dolphin,crab,lobster,squid,octopus,jellyfish,seahorse,coral,sea,ocean,anchor,clam,seal,mermaid,boat,ship"),
        new("Reptiles & Insects", "snake,lizard,turtle,crocodile,frog,insect,spider,scorpion,beetle,butterfly,bee,wasp,ant,fly,mosquito,worm,dragonfly,chameleon"),
        new("Plants & Fungi", "tree,plant,flower,leaf,mushroom,root,seed,bush,cactus,vine,fern,grass,branch,blossom,thorn,ivy,herb"),
        new("Elements & Natural Forces", "fire,water,earth,air,ice,lightning,flame,lava,wind,storm,volcano,wave,stone,rock,crystal,earthquake,frost,steam,energy"),
        new("Weather & Sky", "cloud,rain,snow,wind,storm,tornado,hurricane,fog,rainbow,weather,lightning,thunder,blizzard,cyclone"),
        new("Sun, Moon & Stars", "sun,moon,star,eclipse,constellation,zodiac,night,day,celestial,starlight"),
        new("Space & Astronomy", "space,planet,galaxy,comet,asteroid,rocket,astronaut,satellite,spaceship,alien,orbit,telescope,meteor,cosmos,deathstar"),
        new("Food & Ingredients", "food,fruit,vegetable,meat,bread,cheese,egg,cake,pie,pizza,burger,apple,carrot,corn,meal,sausage,fish,grain,berry"),
        new("Cooking & Kitchen", "cook,cooking,kitchen,pot,pan,oven,stove,kettle,knife,fork,spoon,plate,bowl,chef,recipe,cutting,ladle,whisk"),
        new("Drinks & Vessels", "drink,bottle,cup,mug,glass,flask,potion,water,wine,beer,coffee,tea,barrel,jug,tankard,gallon,thermos"),
        new("Tools & Workshop", "hammer,wrench,saw,drill,tool,anvil,screwdriver,pliers,chisel,vise,toolbox,gear,bolt,screw,nail,clamp,pickaxe,shovel,spade,workbench"),
        new("Crafting & Sewing", "sewing,needle,thread,yarn,knit,fabric,cloth,scissor,loom,craft,paintbrush,palette,glue,spool,embroidery,stitch,tailor"),
        new("Machines & Mechanisms", "machine,engine,motor,mechanism,cog,wheel,pulley,piston,turbine,generator,clockwork,pump,lever,crank,gear,mechanical"),
        new("Robots & Cyborgs", "robot,cyborg,android,mech,servo,drone,robotic,golem,automaton"),
        new("Vehicles & Roads", "car,truck,bus,van,motorcycle,bicycle,bike,wagon,cart,wheel,road,traffic,taxi,ambulance,tractor,vehicle,tire,steering,subway,train,railway"),
        new("Ships & Sailing", "ship,boat,sail,anchor,mast,rudder,submarine,fishing,ferry,canoe,kayak,yacht,pirate,hull,propeller,drakkar,shipwreck"),
        new("Buildings & Places", "house,building,castle,tower,bridge,temple,church,school,hospital,shop,tavern,inn,city,village,dungeon,fort,gate,lighthouse,ruin,street,market"),
        new("Home & Furniture", "chair,table,bed,sofa,couch,cabinet,shelf,lamp,door,window,wardrobe,cupboard,fireplace,armchair,desk,home,household"),
        new("Royalty & Kingdoms", "king,queen,crown,throne,prince,princess,scepter,royal,crest,heraldry,banner,coat,kingdom,emperor,empress,duke"),
        new("Military & Warfare", "war,battle,soldier,army,warrior,tank,military,combat,warship,bunker,battlefield,medal,troop,weapon,rifle,cannon,fort"),
        new("Crime & Law", "thief,robber,crime,law,justice,police,jail,prison,wanted,handcuff,gavel,court,judge,criminal,spy,lockpick,arrest"),
        new("Money & Trade", "coin,money,gold,bank,cash,currency,market,trade,merchant,shop,purchase,sale,treasure,jewel,diamond,ruby,price,wealth"),
        new("Gems & Treasure", "gem,jewel,diamond,ruby,emerald,sapphire,crystal,treasure,chest,loot,pearl,gold,nugget,ring,necklace"),
        new("Cards & Dice", "card,dice,tarot,poker,deck,playing,suit,club,heart,spade,diamond,ace,jack,king,queen"),
        new("Chess & Board Games", "chess,pawn,bishop,rook,knight,board,checker,meeple,domino,boardgame,gamepiece,token"),
        new("Sports & Athletics", "sport,ball,football,basketball,baseball,soccer,golf,tennis,hockey,running,run,swim,boxing,wrestling,medal,trophy,field,player,fitness,exercise,skate,ski"),
        new("Music & Instruments", "music,musical,instrument,guitar,piano,violin,drum,flute,trumpet,saxophone,harp,note,score,microphone,headphone,speaker,song,singer,accordion"),
        new("Books & Writing", "book,scroll,paper,pen,pencil,quill,writing,write,notebook,letter,parchment,ink,library,bookmark,document,file,typewriter,contract"),
        new("Science & Chemistry", "science,chemistry,chemical,atom,molecule,lab,laboratory,experiment,dna,cell,microscope,beaker,flask,physics,formula,math,measure,testtube"),
        new("Computers & Electronics", "computer,laptop,keyboard,monitor,screen,phone,smartphone,tablet,circuit,chip,electronic,battery,usb,server,wifi,network,data,digital,printer,mouse,processor"),
        new("Communication & Media", "communication,signal,radio,phone,telephone,mail,message,chat,megaphone,microphone,newspaper,television,tv,broadcast,antenna,satellite,camera,photo,video,headphone"),
        new("Time & Calendars", "time,clock,watch,hourglass,calendar,timer,sundial,alarm,date,clockwork,year,month,minute,second"),
        new("Travel & Navigation", "map,compass,navigation,travel,backpack,luggage,suitcase,passport,ticket,route,direction,location,marker,journey,explorer,adventure,camp,mountain"),
        new("Locks & Security", "lock,key,padlock,security,safe,vault,alarm,trap,chain,handcuff,guard,shield,watchtower,barred,closed"),
        new("Religion & Mythology", "holy,angel,devil,god,goddess,religion,cross,church,temple,prayer,saint,halo,bible,grail,sacred,symbol,ankh,pentagram,altar,faith"),
        new("Flags & Heraldry", "flag,banner,emblem,crest,badge,shield,insignia,symbol,logo,coat,arms,heraldic,standard,pennant"),
        new("Light & Shadow", "light,lamp,sun,glow,flashlight,candle,lantern,darkness,shadow,bright,radiant,flare,beam,torch,fire"),
        new("Abstract & Geometry", "abstract,spiral,pattern,geometric,circle,triangle,square,polygon,shape,fractal,symmetry,mandala,grid,curve,lines"),
        new("Clothing & Accessories", "hat,clothing,coat,dress,shirt,pants,boot,shoe,glove,glasses,mask,cape,ring,necklace,bracelet,belt,backpack,scarf,helmet,hood,robe"),
    ];

    public static IReadOnlyList<IconPreset> Create(FieldInfo[] fields)
    {
        var allIcons = fields.Select(field => field.Name).ToArray();
        var presets = new List<IconPreset> { new("All Icons", allIcons) };
        foreach (var definition in Definitions)
        {
            var terms = definition.Terms.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var icons = fields
                .Where(field => Tokenize(field.Name).Any(terms.Contains))
                .Select(field => field.Name)
                .ToArray();
            if (icons.Length > 0)
                presets.Add(new IconPreset(definition.Name, icons));
        }

        return presets;
    }

    private static IEnumerable<string> Tokenize(string iconName) =>
        Regex.Split(iconName, "(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])|_")
            .Where(token => token.Length > 1)
            .Select(token => token.ToLowerInvariant());
}
