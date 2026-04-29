using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NovaRetail.Services
{
    /// <summary>
    /// Diccionario centralizado de sinónimos, stop words y lógica de expansión
    /// para la búsqueda inteligente de productos.
    /// </summary>
    public static class SearchSynonyms
    {
        // ─── Stop words ───────────────────────────────────────────────
        public static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "de", "del", "la", "las", "el", "los", "un", "una", "unos", "unas",
            "en", "con", "sin", "para", "por", "al", "y", "o", "e", "a",
            "que", "es", "se", "su", "no", "mas", "como"
        };

        // ─── Grupos de sinónimos ──────────────────────────────────────
        public static readonly string[][] SynonymGroups = new[]
        {
            // ── Unidades de medida ──
            new[] { "m", "mt", "mts", "metro", "metros" },
            new[] { "l", "lt", "lts", "litro", "litros" },
            new[] { "kg", "kgs", "kilo", "kilos", "kilogramo", "kilogramos" },
            new[] { "g", "gr", "grs", "gramo", "gramos" },
            new[] { "cm", "cms", "centimetro", "centimetros" },
            new[] { "mm", "milimetro", "milimetros" },
            new[] { "pul", "pulg", "plg", "pulgada", "pulgadas" },
            new[] { "gal", "galon", "galones" },
            new[] { "lb", "lbs", "libra", "libras" },
            new[] { "oz", "onza", "onzas" },
            new[] { "ml", "mililitro", "mililitros" },
            new[] { "cc", "centimetro cubico" },
            new[] { "pza", "pzas", "pieza", "piezas" },
            new[] { "und", "uds", "unidad", "unidades" },
            new[] { "rollo", "rllo", "rollos" },
            new[] { "paq", "paquete", "paquetes" },
            new[] { "cja", "caja", "cajas" },
            new[] { "doc", "docena", "docenas" },
            new[] { "par", "pares" },
            new[] { "yd", "yds", "yarda", "yardas" },
            new[] { "ft", "pie", "pies" },

            // ── Dulces, snacks y confitería ──
            new[] { "chocolate", "choco", "chocolat" },
            new[] { "galleta", "galletas", "galletita", "galletitas", "cookie", "cookies" },
            new[] { "confite", "confites", "dulce", "dulces", "caramelo", "caramelos", "gomita", "gomitas" },
            new[] { "chicle", "chicles", "goma de mascar" },
            new[] { "tapita", "tapitas", "tapa", "tapas" },
            new[] { "wafer", "waffer", "oblea" },
            new[] { "paleta", "paletas", "chupeta", "chupetas" },
            new[] { "mani", "cacahuate", "cacahuates" },
            new[] { "papa", "papas", "chip", "chips", "snack", "snacks" },
            new[] { "nachos", "nacho", "tortilla chip" },

            // ── Bebidas ──
            new[] { "refresco", "gaseosa", "soda" },
            new[] { "jugo", "jugos", "zumo", "nectar" },
            new[] { "cerveza", "birra", "beer" },
            new[] { "agua", "h2o" },
            new[] { "energizante", "energy drink", "bebida energetica" },
            new[] { "vino", "wine" },
            new[] { "ron", "rum" },
            new[] { "vodka", "whisky", "whiskey" },

            // ── Lácteos y frescos ──
            new[] { "leche", "lacteo", "milk" },
            new[] { "queso", "qso", "cheese" },
            new[] { "yogurt", "yogur", "yoghurt" },
            new[] { "mantequilla", "margarina", "butter" },
            new[] { "natilla", "crema dulce" },
            new[] { "huevo", "huevos" },

            // ── Abarrotes / despensa ──
            new[] { "arroz", "arz", "rice" },
            new[] { "frijol", "frijoles", "bean", "beans" },
            new[] { "azucar", "azucr", "endulzante", "sugar" },
            new[] { "aceite", "aceit", "oil" },
            new[] { "sal", "sales" },
            new[] { "harina", "har", "flour" },
            new[] { "cafe", "coffee" },
            new[] { "te", "tea", "infusion" },
            new[] { "atun", "tuna" },
            new[] { "salsa", "salsas", "adereso", "aderezo" },
            new[] { "mayonesa", "mayo" },
            new[] { "mostaza", "mustard" },
            new[] { "ketchup", "salsa tomate", "catsup" },
            new[] { "cereal", "cereales" },
            new[] { "avena", "oat", "oats" },
            new[] { "spaghetti", "espagueti", "pasta alimenticia", "fideo", "fideos" },
            new[] { "pan", "bread" },
            new[] { "tortilla", "tortillas" },
            new[] { "maiz", "elote", "corn" },
            new[] { "vinagre", "vinegar" },
            new[] { "miel", "honey" },
            new[] { "mermelada", "jalea", "jam" },
            new[] { "gelatina", "jello" },
            new[] { "leche condensada", "condensada" },
            new[] { "leche evaporada", "evaporada" },
            new[] { "polvo hornear", "levadura", "royal" },
            new[] { "canela", "cinnamon" },
            new[] { "pimienta", "pepper" },
            new[] { "consomme", "consome", "caldo", "cubito" },
            new[] { "sardina", "sardinas" },
            new[] { "sopa", "sopas", "crema instantanea" },

            // ── Frutas y verduras ──
            new[] { "tomate", "jitomate" },
            new[] { "cebolla", "onion" },
            new[] { "papa", "patata", "potato" },
            new[] { "banano", "banana", "guineo", "platano" },
            new[] { "manzana", "apple" },
            new[] { "naranja", "orange" },
            new[] { "limon", "lima", "lemon", "lime" },
            new[] { "aguacate", "avocado" },
            new[] { "lechuga", "lettuce" },
            new[] { "zanahoria", "carrot" },
            new[] { "chile", "aji", "picante" },

            // ── Carnes / proteínas ──
            new[] { "pollo", "chicken" },
            new[] { "carne", "res", "beef" },
            new[] { "cerdo", "pork", "chancho" },
            new[] { "jamon", "ham" },
            new[] { "salchicha", "hot dog", "frankfurter" },
            new[] { "chorizo", "embutido" },
            new[] { "mortadela", "bologna" },
            new[] { "pescado", "fish" },

            // ── Limpieza y hogar ──
            new[] { "pasta", "crema" },
            new[] { "jabon", "detergente", "soap" },
            new[] { "desinfectante", "antibacterial", "germicida" },
            new[] { "cloro", "lejia", "blanqueador", "clorox" },
            new[] { "suavizante", "suavitel" },
            new[] { "trapeador", "mopa", "mop" },
            new[] { "esponja", "fibra", "estropajo" },
            new[] { "papel higienico", "papel hig", "higienico" },
            new[] { "servilleta", "servilletas", "napkin" },
            new[] { "toalla", "toallas" },
            new[] { "bolsa", "funda", "bolsas" },
            new[] { "escoba", "cepillo", "broom" },
            new[] { "balde", "cubeta", "bucket" },
            new[] { "guante", "guantes" },
            new[] { "insecticida", "mata cucarachas", "raid" },
            new[] { "ambientador", "aromatizante", "air freshener" },
            new[] { "lustramuebles", "limpia muebles", "pledge" },
            new[] { "limpia vidrios", "glass cleaner" },
            new[] { "bolsa basura", "bolsa desecho" },

            // ── Cuidado personal ──
            new[] { "desodorante", "antitranspirante", "deodorant" },
            new[] { "champu", "shampoo" },
            new[] { "acondicionador", "rinse", "conditioner" },
            new[] { "pasta dental", "crema dental", "dentifrico" },
            new[] { "cepillo dental", "cepillo dientes" },
            new[] { "hilo dental", "floss" },
            new[] { "enjuague bucal", "listerine" },
            new[] { "rastrillo", "rasuradora", "afeitadora", "gillette" },
            new[] { "toalla sanitaria", "toalla femenina", "kotex" },
            new[] { "panal", "panales", "diaper" },
            new[] { "algodon", "cotton" },
            new[] { "alcohol", "alcohol en gel" },
            new[] { "bloqueador", "protector solar", "sunscreen" },
            new[] { "crema corporal", "locion", "body lotion" },
            new[] { "perfume", "colonia", "fragancia" },
            new[] { "talco", "polvo" },

            // ── Ferretería ──
            new[] { "clavo", "clavos", "nail", "nails" },
            new[] { "tornillo", "tornillos", "screw" },
            new[] { "tuerca", "tuercas", "nut" },
            new[] { "manguera", "mang", "hose" },
            new[] { "alambre", "cable", "wire" },
            new[] { "pintura", "pint", "paint" },
            new[] { "cerradura", "chapa", "lock" },
            new[] { "llave", "grifo", "faucet" },
            new[] { "tubo", "tuberia", "cano", "pipe" },
            new[] { "cuerda", "mecate", "soga", "rope" },
            new[] { "silicon", "silicone", "silicona" },
            new[] { "desarmador", "destornillador", "screwdriver" },
            new[] { "lija", "papel lija", "sandpaper" },
            new[] { "broca", "brocas", "drill bit" },
            new[] { "cinta", "tape", "masking" },
            new[] { "martillo", "mazo", "hammer" },
            new[] { "sierra", "serrucho", "saw" },
            new[] { "alicate", "alicates", "pinza", "pinzas", "tenaza", "pliers" },
            new[] { "candado", "padlock" },
            new[] { "bisagra", "bisagras", "hinge" },
            new[] { "taladro", "drill" },
            new[] { "cemento", "concreto", "concrete" },
            new[] { "pega", "pegamento", "adhesivo", "glue" },
            new[] { "brocha", "brochas", "paintbrush" },
            new[] { "rodillo", "roller" },
            new[] { "interruptor", "switch", "apagador" },
            new[] { "enchufe", "tomacorriente", "toma", "outlet" },
            new[] { "extension", "regleta" },
            new[] { "pvc", "cpvc" },
            new[] { "varilla", "hierro", "rebar" },
            new[] { "foco", "bombillo", "bombilla", "bujia", "led", "bulb" },
            new[] { "nivel", "levels" },
            new[] { "cinta metrica", "metro", "flexometro" },
            new[] { "llave inglesa", "wrench", "adjustable" },
            new[] { "soldadura", "estano", "solder" },
            new[] { "thinner", "diluyente", "solvente" },
            new[] { "impermeabilizante", "sellador" },
            new[] { "barniz", "varnish", "laquer" },
            new[] { "esmeril", "grinder" },
            new[] { "disco", "disco corte" },
            new[] { "cadena", "chain" },
            new[] { "gancho", "ganchos", "hook" },
            new[] { "abrazadera", "clamp" },
            new[] { "resina", "epoxy", "epoxico" },
            new[] { "chazos", "taquete", "ancla" },
            new[] { "arandela", "washer" },

            // ── Eléctrico ──
            new[] { "breaker", "breker", "disyuntor" },
            new[] { "cable electrico", "cable thhn" },
            new[] { "canaleta", "tubo conduit" },
            new[] { "fusible", "fuse" },
            new[] { "socket", "portalampara" },
            new[] { "cinta aislante", "tape electrico" },

            // ── Plomería ──
            new[] { "llave paso", "valvula", "valve" },
            new[] { "teflon", "cinta teflon" },
            new[] { "sifon", "trampa" },
            new[] { "flapper", "sapito" },
            new[] { "flotador", "float" },
            new[] { "codo", "elbow" },
            new[] { "tee", "union" },
            new[] { "adaptador", "adapter" },
            new[] { "reduccion", "reductor", "reducer" },

            // ── Papelería y oficina ──
            new[] { "lapiz", "lapices", "pencil" },
            new[] { "boligrafo", "lapicero", "pluma", "pen" },
            new[] { "cuaderno", "libreta", "notebook" },
            new[] { "folder", "carpeta" },
            new[] { "grapadora", "engrapadora", "stapler" },
            new[] { "grapa", "grapas", "staple" },
            new[] { "tijera", "tijeras", "scissors" },
            new[] { "borrador", "goma borrar", "eraser" },
            new[] { "corrector", "liquid paper" },
            new[] { "regla", "ruler" },
            new[] { "marcador", "marcadores", "marker" },
            new[] { "resaltador", "highlighter" },
            new[] { "papel bond", "papel carta", "letter paper" },
            new[] { "sobre", "sobres", "envelope" },
            new[] { "clip", "clips" },
            new[] { "chinche", "tachuela", "pushpin" },
            new[] { "pegamento barra", "pritt", "glue stick" },

            // ── Mascotas ──
            new[] { "alimento", "comida", "food" },
            new[] { "perro", "can", "mascota", "dog" },
            new[] { "gato", "felino", "cat" },
            new[] { "arena gato", "cat litter" },
            new[] { "collar", "correa", "leash" },
            new[] { "antipulgas", "pulguicida" },

            // ── Textiles / ropa ──
            new[] { "hilo", "hilos", "thread" },
            new[] { "aguja", "agujas", "needle" },
            new[] { "zipper", "cierre", "ziper" },
            new[] { "boton", "botones", "button" },
            new[] { "elastico", "resorte" },
            new[] { "tela", "fabric" },
            new[] { "prensa ropa", "pinza ropa", "gancho ropa" },

            // ── Jardín / agricultura ──
            new[] { "abono", "fertilizante", "fertilizer" },
            new[] { "tierra", "sustrato", "soil" },
            new[] { "maceta", "matera", "pot" },
            new[] { "herbicida", "mata maleza", "roundup" },
            new[] { "fumigador", "sprayer" },
            new[] { "malla", "cedazo", "mesh" },

            // ── Automotriz ──
            new[] { "aceite motor", "lubricante", "motor oil" },
            new[] { "filtro", "filtros", "filter" },
            new[] { "anticongelante", "coolant", "refrigerante" },
            new[] { "liquido frenos", "brake fluid" },
            new[] { "limpia parabrisas", "wiper fluid" },
            new[] { "llanta", "neumatico", "tire" },
            new[] { "bateria", "acumulador", "battery" },
        };

        // ─── Lookup cache ─────────────────────────────────────────────
        private static Dictionary<string, string[]>? _synonymLookup;

        public static Dictionary<string, string[]> GetSynonymLookup()
        {
            if (_synonymLookup != null) return _synonymLookup;
            var lookup = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in SynonymGroups)
                foreach (var word in group)
                    lookup[word] = group;
            _synonymLookup = lookup;
            return lookup;
        }

        /// <summary>
        /// Expande cada palabra en variantes (sinónimos, número+unidad).
        /// </summary>
        public static List<List<string>> ExpandSearchWords(string[] words)
        {
            var syns = GetSynonymLookup();
            var result = new List<List<string>>();

            for (int idx = 0; idx < words.Length; idx++)
            {
                var word = words[idx];
                var variants = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { word };

                var m = Regex.Match(word, @"^(\d+[\.,]?\d*)([a-zA-Z]+)$");
                if (m.Success)
                {
                    AddNumUnitVariants(variants, m.Groups[1].Value, m.Groups[2].Value, syns);
                }
                else if (Regex.IsMatch(word, @"^\d+[\.,]?\d*$") && idx + 1 < words.Length
                         && syns.ContainsKey(words[idx + 1]))
                {
                    var num = word;
                    var unit = words[idx + 1];
                    variants.Add(unit);
                    AddNumUnitVariants(variants, num, unit, syns);
                    idx++;
                }
                else if (syns.TryGetValue(word, out var group))
                {
                    foreach (var s in group)
                        if (s.Length >= 2) variants.Add(s);
                }

                result.Add(variants.ToList());
            }

            return result;
        }

        /// <summary>
        /// Filtra stop words del array. Si todas son stop words, devuelve el original.
        /// </summary>
        public static string[] RemoveStopWords(string[] words)
        {
            var filtered = words.Where(w => !StopWords.Contains(w)).ToArray();
            return filtered.Length > 0 ? filtered : words;
        }

        /// <summary>
        /// Verifica si <paramref name="word"/> aparece como palabra completa
        /// dentro de <paramref name="text"/> (delimitada por caracteres no alfanuméricos).
        /// Evita falsos positivos como "cola" dentro de "chocolate".
        /// </summary>
        public static bool ContainsWord(string text, string word)
        {
            int idx = 0;
            while (true)
            {
                idx = text.IndexOf(word, idx, StringComparison.OrdinalIgnoreCase);
                if (idx < 0) return false;

                bool startOk = idx == 0 || !char.IsLetterOrDigit(text[idx - 1]);
                int end = idx + word.Length;
                bool endOk = end >= text.Length || !char.IsLetterOrDigit(text[end]);

                if (startOk && endOk) return true;
                idx++;
            }
        }

        private static void AddNumUnitVariants(HashSet<string> variants, string num, string unit, Dictionary<string, string[]> syns)
        {
            variants.Add(num + unit);
            variants.Add(num + " " + unit);
            if (syns.TryGetValue(unit, out var group))
            {
                foreach (var s in group)
                {
                    variants.Add(num + s);
                    variants.Add(num + " " + s);
                }
            }
        }
    }
}
