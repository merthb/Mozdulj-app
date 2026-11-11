"""Idempotent v0.22 yoga library expansion.

Original Hungarian instruction text created for Mozdulj!, informed by publicly
accessible pose indices (YogaBasics and Yoga Journal), NOT copied translations.
All new 3D rigs are illustrative editable geometric starts and require review.
"""
import copy, json, pathlib
P = pathlib.Path(__file__).resolve().parents[1] / 'Mozdulj.Maui/Resources/Raw/exercises.catalog.json'
DB = json.loads(P.read_text(encoding='utf-8'))
byid = {x['id']: x for x in DB['exercises']}
# id | Hungarian name | base exemplar | category | region list | phase | difficulty |
# seconds | position | main instruction | continuation | safety | 3D motion hint
DATA = '''
pranama-tadasana|Imatartás állásban – Pranamasana|hegytartas|egyensúly|váll,törzs|warmup|1|40|Állj csípőszélességben, a térdek ne zárjanak hátra.|Hozd össze a tenyereket a szegycsont előtt, a vállak maradjanak lazák.|Figyeld meg a testsúlyt mindkét talpon, majd engedd le a karokat.|Szédüléskor támaszkodj meg.|hands_prayer
parvatasana-ules|Ülő hegytartás, karok magasban – Parvatasana|pillangoules|nyújtás|váll,hát,törzs|cooldown|1|45|Ülj kényelmesen párnán vagy jógamatracon, a gerinc hosszú.|Nyújtsd a karokat a fej fölé, tenyerek egymás felé; a vállak ne húzódjanak a fülekhez.|Nyújtózz csak addig, ameddig kényelmes, majd engedd vissza a karokat.|Vállpanasznál emeld a karokat alacsonyabbra.|arms_up
sukhasana-oldalhajlas|Kényelmes ülés oldalsó nyújtózással – Sukhasana|pillangoules|nyújtás|hát,derék,váll|cooldown|1|60|Ülj kényelmes keresztezett lábbal vagy párnán.|Az egyik kezet tedd a talajra, a másik karral nyújtózz a fej fölött oldalra.|Lélegezz nyugodtan, emelkedj vissza, majd válts oldalt.|Ne nyomd össze a derekat, és ne feszítsd a térdet.|arms_side
uttanasana-terdhajlitva|Álló előrehajlás hajlított térddel – Uttanasana|ardha-uttanasana|nyújtás|hát,comb,csípő|cooldown|1|50|Állj csípőszélességben, a térdek enyhén hajlítva.|Csípőből indulva engedd a törzset előre és le, a karokat lazítsd.|A térdeket annyira hajlíthatod, hogy a derék ne feszüljön; lassan gördülj fel.|Szédülés és derékfájdalom esetén hagyd ki a mély előrehajlást.|forward
prasarita-padottanasana|Széles terpeszes előrehajlás – Prasarita Padottanasana|haromszogpoz-konnyitett|nyújtás|comb,csípő,hát|cooldown|2|60|Állj stabil, kényelmes széles terpeszbe, lábfejek előre.|Tartsd hosszúnak a hátad, és csípőből dönts előre, a kezed lehet egy szék támláján.|Maradj néhány légzésig, majd a lábakból emelkedj vissza.|Vérnyomásproblémánál vagy szédüléskor használj magas támaszt.|forward
malasana-szekkel|Jógaguggolás széktámasszal – Malasana|felguggolas-szekkel|mobilizálás|csípő,comb,boka|main|2|50|Állj szélesebb terpeszben, tarts meg egy stabil széket.|Lassan hajlítsd a térdeket, engedd a csípőt kényelmes mélységbe; a sarok maradhat alátámasztva.|Emeld meg a mellkast, majd lassan állj fel.|Térd- vagy bokafájdalomnál kisebb guggolást válassz.|squat
virabhadrasana-iii-fal|Harcos III falnál – Virabhadrasana III könnyítve|fapoz-tamasz-mellett|egyensúly|csípő,comb,törzs|main|2|55|Állj a falnál, a tenyereidet támaszd meg vállmagasságban.|Csípőből döntve nyújtsd hátra az egyik lábat, csak ameddig a medencéd stabil marad.|Tartsd rövid ideig, majd lassan térj vissza, cserélj lábat.|Derékfájdalom vagy bizonytalan egyensúly esetén ne emeld magasra a lábat.|leg_back
utthita-trikonasana-szek|Háromszögpóz kéztámasszal – Trikonasana|haromszogpoz-konnyitett|nyújtás|comb,csípő,hát|cooldown|2|60|Állj kényelmes terpeszbe, az elöl álló lábfej kifelé néz.|A csípőt hátratolva nyújtózz előre, majd a kezedet tedd a combodra vagy székhez.|A felső vállat óvatosan nyisd, a nyak maradjon kényelmes.|A térdet ne feszítsd hátra; derékpanasznál kisebb mozdulatot végezz.|side_reach
parivrtta-utkatasana|Széktartás finom csavarással – Parivrtta Utkatasana könnyítve|utkatasana|mobilizálás|törzs,csípő,comb|main|2|45|Állj csípőszélességben, majd hajlítsd kissé a térded.|Tedd a kezed a mellkas elé, és a mellkasból fordulj néhány fokot jobbra.|Térj középre, majd fordulj balra, a medence előre néz.|Gerincpanasznál csavarás nélkül végezd a széktartást.|upper_twist
utthita-parsvottanasana-szek|Piramispóz magas támasszal – Parsvottanasana|parsvottanasana|nyújtás|comb,csípő,hát|cooldown|1|60|Lépj kis terpeszállásba, mindkét csípő előre néz.|Az elöl lévő láb fölé dönts csípőből, és támaszd meg a kezed egy stabil széken.|Tartsd a hátat hosszúnak, majd oldalcsere következik.|A térdeket nem kell teljesen kinyújtani.|forward
utkata-konasana-karok|Istennőpóz karfelnyitással – Utkata Konasana változat|utkata-konasana|erősítés|comb,csípő,váll|main|2|50|Állj szélesebb terpeszbe, a lábfejek enyhén kifelé forduljanak.|Hajlítsd a térdeket a lábfejek irányába, a karokat oldalra és felfelé nyisd.|Tartsd rövid ideig, majd nyújtsd a lábad, és engedd le a karokat.|Ne engedd befelé a térdeket, a vállak maradjanak lazák.|arms_side
vrksasana-labujj|Fapóz lábujjtámasszal – Vrksasana kezdő|fapoz-tamasz-mellett|egyensúly|boka,comb,csípő|main|1|45|Állj fal mellett, egyik talpadon stabilan.|A másik láb ujjait támaszd a földre, és a sarkát tedd a belső bokád mellé.|Hozd a tenyereket a mellkas elé, majd cserélj oldalt.|A talpat ne nyomd az álló láb térdéhez.|balance
utthita-hasta-padangusthasana-szek|Nyújtott láb emelése széknél – Utthita Hasta Padangusthasana könnyítve|egy-labon-allas-tamasz-mellett|egyensúly|comb,csípő,törzs|main|2|45|Állj stabil szék vagy fal mellett, egyik kezed támaszkodik.|Lassan emeld a másik térded, majd csak kényelmesen nyújtsd előre a lábszárad.|Tartsd meg, engedd vissza, és végezd a másik oldalon.|Ne erőltesd a térd teljes nyújtását vagy a magas lábemelést.|leg_front
adho-mukha-svanasana-szek|Lefelé néző kutya széktámaszon – Adho Mukha Svanasana|falnal-lefele-kutya|nyújtás|váll,hát,comb|cooldown|1|60|Állj stabil székkel szemben, és tedd a kezed a támlára.|Lépj hátra, csípőből hajolj előre, a kar és a hát hosszú vonalat alkosson.|Tartsd a térdeket enyhén hajlítva, majd lassan lépj vissza.|A szék ne csússzon; vállfájdalomnál kerüld a mély támaszt.|forward
virabhadrasana-i-karok|Harcos I karokkal – Virabhadrasana I|harcos-i-rovid-tartassal|erősítés|comb,csípő,váll|main|2|50|Lépj kényelmes harántterpeszbe, az elöl lévő térd lábfej fölött.|A törzs hosszú marad, a karokat emeld a magasba, ha kényelmes.|Lassan engedd le, majd a másik lábbal ismételd.|Ne homoríts túl, és ne erőltesd a hátsó térdet.|arms_up
virabhadrasana-ii-karok|Harcos II karok oldalra nyújtva – Virabhadrasana II|harcos-ii-modositva|erősítés|comb,csípő,váll|main|2|55|Állj széles, stabil terpeszbe, egyik lábfej kifelé fordul.|Hajlítsd az elöl lévő térdet, a két kart vízszintesen nyújtsd szét.|Nézz a mellső kéz irányába, majd óvatosan váltasz oldalt.|A térd maradjon a lábfej irányában; a váll ne emelkedjen fel.|arms_side
purvottanasana-keszul|Fordított asztaltartás előkészítése – Ardha Purvottanasana|hid-csipoemelessel|erősítés|váll,farizom,törzs|main|2|45|Ülj hajlított térddel, kezek a csípő mögött, ujjak kényelmes irányban.|Támaszkodj a talpon és tenyéren, majd kis mértékben emeld a medencéd.|Engedd vissza lassan, és rázd le a csuklódat.|Csukló- vagy vállfájdalomnál hagyd ki a támaszt.|bridge
ardha-pincha-mayurasana|Delfinpóz rövid tartással – Ardha Pincha Mayurasana|alkartamasz-terden|erősítés|váll,törzs,comb|main|2|40|Helyezkedj négykézlábra, majd a könyöködet tedd vállszélességben a matracra.|Emeld a csípőt óvatosan felfelé, a térd maradhat hajlítva.|Tartsd pár légzésig, majd térj vissza térdelésbe.|Váll- vagy nyakpanasznál maradj az alkartámasz előkészítésénél.|tabletop
bharmanasana-vallemeles|Négykézláb váltott karnyújtás – Bharmanasana változat|negykezlab-kartavolitas|erősítés|váll,törzs,hát|main|1|45|Helyezkedj négykézlábra, a tenyér a váll alatt, a térd a csípő alatt.|Nyújtsd előre az egyik kart úgy, hogy a medence ne billenjen.|Tedd le lassan, majd válts kart.|Csuklónál használhatsz támaszt vagy ökölre támaszkodást.|arms_up
vajrasana-parnaval|Térdelő ülés alátámasztással – Vajrasana|terdelo-piheno-jogablokkal|relaxáció|csípő,comb,boka|cooldown|1|60|Térdelj puha matracra, helyezz párnát vagy blokkot az ülőcsont alá.|Engedd a testsúlyt az alátámasztásra, a gerinc maradjon hosszú.|Néhány nyugodt légzés után a kezek segítségével kelj fel.|Térd- vagy bokapanasznál válassz más ülőhelyzetet.|static
virasana-parnaval|Hőspóz párnán – Virasana könnyítve|terdelo-piheno-jogablokkal|nyújtás|comb,boka,csípő|cooldown|2|45|Térdelj kényelmesen, az ülőcsontok alá tegyél magas párnát.|Csak akkora térdhajlításban ülj le, amely fájdalommentes.|Tartsd a törzsed függőlegesen, majd kézzel támaszkodva emelkedj fel.|Térdsérülés, meniszkuszpanasz vagy bokafájdalom esetén ne végezd.|static
agnistambhasana-kimelo|Kímélő csípőnyitás ülve – Agnistambhasana előkészítés|pillangoules|nyújtás|csípő,farizom|cooldown|2|45|Ülj magas párnára, lábak kényelmes keresztezésben.|A bokát csak akkor helyezd a másik combra, ha a térdnek ez teljesen kényelmes.|Tartsd egyenesebben a hátat, majd lassan válts oldalt.|Térdfájdalom esetén maradj egyszerű keresztezett ülésben.|static
baddha-konasana-allo|Pillangóülés előredöntéssel – Baddha Konasana változat|pillangoules|nyújtás|csípő,comb,hát|cooldown|1|50|Ülj a matracon, a talpak egymás felé, a térdek oldalra.|Hosszú gerinccel csípőből indulva nagyon kicsit dőlj előre.|Lélegezz nyugodtan, majd lassan emelkedj vissza.|Ne nyomd kézzel a térdeket a talajhoz.|forward
upavistha-konasana-szek|Széles terpeszes ülő nyújtás – Upavistha Konasana könnyítve|ulo-elorehajlas-jogapanttal|nyújtás|comb,csípő,hát|cooldown|2|60|Ülj le a matracra, terpeszd a lábakat csak kényelmes szélességig.|Térdek lehetnek hajlítottak; a kezedet a talajra támasztva csípőből dőlj enyhén előre.|Néhány légzés után a karokkal segítsd vissza a törzsed.|Ne húzd túl a comb belső részét.|forward
parivrtta-janu-sirsasana|Ülő oldalra nyújtózás – Parivrtta Janu Sirsasana könnyítve|janu-sirsasana|nyújtás|derék,hát,comb|cooldown|2|55|Ülj egyik lábbal kényelmesen nyújtva, másik térded hajlítva.|Az egyik alkaroddal támaszkodj, a másikkal ívben nyújtózz a fejed fölött.|Ne csavarj túl mélyre; lassan térj vissza, majd oldalcsere.|Derékfájdalom esetén csökkentsd a törzshajlítást.|side_reach
setu-bandha-karok|Hídpóz karokkal a test mellett – Setu Bandha Sarvangasana|hid-csipoemelessel|erősítés|farizom,törzs,comb|main|1|45|Feküdj hanyatt, hajlítsd a térdeket és tedd le a talpakat.|A karok nyugodtan maradnak oldalt, a medencét lassan emeld a csípőből.|Rövid tartás után csigolyáról csigolyára engedd le a hátad.|Nyaki fájdalomnál ne fordítsd a fejed tartás közben.|bridge
supta-matsyendrasana-kimelo|Hanyatt fekvő térdek oldalra – Supta Matsyendrasana könnyítve|gerinccsavaras-hanyatt|mobilizálás|derék,csípő,hát|cooldown|1|60|Feküdj hanyatt, térdek hajlítva, talpak a földön.|Engedd mindkét térdet kis ívben oldalra, a vállak maradjanak lent.|Vissza középre, majd a másik oldalra ismételd.|Csak fájdalommentes tartományban csavarj.|twist
jathara-parivartanasana-valtott|Hanyatt térddöntés váltva – Jathara Parivartanasana|gerinccsavaras-hanyatt|mobilizálás|derék,törzs,csípő|warmup|1|50|Feküdj hanyatt hajlított térdekkel, talpak támaszon.|Kilégzésre térdeidet lassan döntsd az egyik oldalra néhány centire.|Belégzésre középre, majd fordítsd őket a másik irányba.|Kerüld az erőltetett derékcsavarást.|twist
pavanamuktasana-fel|Fél szélkieresztő póz – Ardha Pavanamuktasana|terdhuzas-mellkashoz-hanyatt|nyújtás|csípő,derék,farizom|cooldown|1|60|Feküdj hanyatt, mindkét lábad kényelmesen kinyújtva vagy behajlítva.|Húzd az egyik térded a mellkas irányába a comb mögött összekulcsolt kézzel.|Tartsd néhány légzésig, majd lazán cserélj lábat.|Térdpanasznál a térdhajlat mögött támaszd a lábat.|knee_up
supta-padangusthasana-pant|Hanyatt lábnyújtás pánttal – Supta Padangusthasana|combhajlito-nyujtas-torolkozovel|nyújtás|comb,csípő,vádli|cooldown|1|60|Feküdj hanyatt, az egyik talpad maradjon lent, a másikhoz használj pántot.|Emeld a másik lábad annyira, hogy enyhe feszülést érezz a comb hátulján.|A térd maradhat hajlított; tartsd, majd válts oldalt.|Ne húzd rántással a pántot, és ne feszítsd hátra a térdet.|leg_front
makarasana-krokodil|Krokodilpihenő hason – Makarasana|szfinxpoz|relaxáció|hát,váll,csípő|cooldown|1|60|Feküdj hasra, a homlokodat tedd keresztbe tett alkarokra.|Engedd el a vállakat és a farizmokat, a lábak lazán kifelé fordulhatnak.|Lélegezz a hasfal felé finoman, majd oldalra fordulva gyere ki.|Ha hason fekvés kényelmetlen, inkább oldalfekvésben pihenj.|static
salabhasana-kar-hatra|Sáskapóz karok hátra nyújtva – Salabhasana változat|salabhasana-konnyitett|erősítés|hát,farizom,váll|main|2|35|Feküdj hasra, a karokat tedd a test mellé, a homlok nézzen lefelé.|Emeld meg csak kicsit a mellkast, a karok hátrafelé nyújtóznak.|Tartsd pár másodpercig, majd lassan engedd vissza.|Derékpanasznál az emelés legyen minimális vagy maradjon el.|back_extension
ardha-dhanurasana|Fél íjpóz előkészítése – Ardha Dhanurasana|salabhasana-konnyitett|mobilizálás|comb,csípő,has|main|2|45|Feküdj hasra, az egyik térded hajlítsd kényelmesen.|Ha elérhető, fogd meg az azonos oldali bokát; különben használj pántot.|A comb elején enyhe nyúlást keress, majd engedd vissza és cserélj.|Ne húzd a térdet hátra erőből; derékpanasznál hagyd ki.|knee_bend
bhramari-lelegzet|Zümmögő kilégzés – Bhramari|legzogyakorlat-ulve|légzőgyakorlat|törzs,váll|cooldown|1|60|Ülj kényelmesen, lazítsd el az arcot és a vállakat.|Belégzés után csukott szájjal, erőlködés nélkül zümmögj halkan kilégzés közben.|Néhány ismétlés után térj vissza a természetes légzéshez.|Szédüléskor vagy kellemetlenségnél hagyd abba, ne tartsd vissza a levegőt.|static
nadi-sodhana-kenyelmes|Váltott orrlyukas légzés erőltetés nélkül – Nadi Shodhana|legzogyakorlat-ulve|légzőgyakorlat|törzs,váll|cooldown|1|75|Ülj kényelmesen, hátad támasztható.|Finoman fogd be az egyik orrnyílást, a másikon lélegezz természetesen be és ki.|Válts oldalt; ne számold szigorúan és ne tartsd bent a levegőt.|Orrdugulás vagy szédülés esetén inkább lélegezz mindkét orrnyíláson.|static
dirga-pranayama|Háromrészes, nyugodt légzés – Dirga Pranayama|hasi-legzes-hanyatt|légzőgyakorlat|törzs,hát|cooldown|1|75|Feküdj kényelmesen hanyatt, térdek alá kerülhet párna.|Figyeld, ahogy a légzés először a hasfalat, majd a bordák oldalsó részét mozgatja.|Kilégzéskor minden külön erőfeszítés nélkül engedd vissza a légzést.|Ne erőltess mély belégzést; légszomjnál állj meg.|static
sukhasana-lassu-legzes|Kényelmes ülés lassú légzéssel – Sukhasana|legzogyakorlat-ulve|légzőgyakorlat|törzs,váll|warmup|1|50|Ülj párnára vagy székre, a hátad hosszú és kényelmes.|Belégzés közben figyeld a mellkas enyhe tágulását, kilégzéskor engedd le a vállakat.|A légzés legyen természetes, ritmusát ne kényszerítsd.|Szédülés esetén a saját normál légzésedhez térj vissza.|static
supta-baddha-konasana-parnaval|Fekvő pillangó párnás térdtámasszal – Supta Baddha Konasana|supta-baddha-konasana|relaxáció|csípő,comb,derék|cooldown|1|75|Feküdj hanyatt, a térdeket két párnával támasszd meg oldalról.|A talpakat közelítsd egymáshoz, a csípő kényelmesen nyitott helyzetben marad.|Lassú légzéssel pihenj, végül kézzel segítsd vissza a térdeket.|A térdeket soha ne nyomd lefelé.|static
savasana-oldalra-fordulva|Oldalfekvő pihenőpóz – Savasana alternatíva|piheno-poz-hanyatt|relaxáció|hát,csípő,törzs|cooldown|1|90|Feküdj kényelmes oldalfekvésbe, a térdek közé tehetsz párnát.|A fejed támaszd alá, a váll és csípő maradjon egymás fölött.|Figyeld meg a légzést, majd nyugodtan emelkedj fel.|Csak kényelmes testhelyzetben maradj.|static
'''.strip()

assert len(DATA.splitlines()) == 39, len(DATA.splitlines())
# Use only exercises already in the app as illustrative reference rig templates.
SOURCE_URLS = ['https://www.yogabasics.com/practice/yoga-postures/', 'https://www.yogajournal.com/poses/library']
MOTION = {
 'hands_prayer': lambda f: f['hands'].update(left=[-.10, 1.45, .30], right=[.10,1.45,.30]),
 'arms_up': lambda f: f['hands'].update(left=[-.22,1.66,.02],right=[.22,1.66,.02]),
 'arms_side': lambda f: f['hands'].update(left=[-.72,1.35,.03],right=[.72,1.35,.03]),
 'side_reach': lambda f: (f.__setitem__('spineBend',.13),f['hands'].update(right=[.45,1.66,.08])),
 'forward': lambda f: f.__setitem__('euler',[.35,0,0]),
 'squat': lambda f: (f['root'].__setitem__(1,.71), f['hands'].update(left=[-.2,1.17,.21],right=[.2,1.17,.21])),
 'leg_back':lambda f: f['feet'].update(right=[.16,.72,.65]),
 'leg_front':lambda f: f['feet'].update(right=[.18,.76,-.40]),
 'upper_twist':lambda f: f.__setitem__('upperYaw',.25),
 'balance':lambda f: f['feet'].update(right=[.15,.54,.13]),
 'bridge':lambda f: f['root'].__setitem__(1,.45),
 'tabletop':lambda f: f['hands'].update(right=[.30,.25,-.60]),
 'twist':lambda f: f.__setitem__('upperYaw',.18),
 'knee_up':lambda f: f['feet'].update(right=[.15,.50,.34]),
 'knee_bend':lambda f: f['feet'].update(right=[.15,.36,.47]),
 'back_extension':lambda f: f.__setitem__('spineBend',.10),
 'static':lambda f:None,
}

added=[]; skipped=[]
for row in DATA.splitlines():
    cols=[t.strip() for t in row.split('|')]
    if len(cols)!=13: raise ValueError((len(cols),cols))
    eid,name,base,category,regions,phase,diff,sec,position,summary,step2,safety,motion = cols
    if eid in byid: skipped.append(eid); continue
    if base not in byid: raise ValueError(f'No representative: {base}')
    e=copy.deepcopy(byid[base]); rig=copy.deepcopy(e['pose']['rig3D'])
    if not rig or not rig.get('frames'):raise ValueError(base)
    first=copy.deepcopy(rig['frames'][0]); first['name']='Kiindulás'
    if motion=='static': rig['frames']=[first]
    else:
        second=copy.deepcopy(first);second['name']='Mozdulat';MOTION[motion](second)
        rig['frames']=[first,second]
    rig['reviewStatus']='illustrative'
    # Matching static or loosely matched rig is deliberately not marked verified.
    e.update({'id':eid, 'revision':1,'name':name,'category':category,'regions':regions.split(','),
              'tags':[],'equipment':copy.deepcopy(e.get('equipment') or []),'difficulty':int(diff),'phase':phase,
              'measure':'time','targetSeconds':int(sec),'repTargets':[],'estimatedSeconds':int(sec),
              'startingPosition':position,'summary':summary,'steps':[summary,step2],
              'commonMistakes':['Erőltetett mozgástartomány vagy fájdalomba nyújtás.','Légzés visszatartása vagy hirtelen irányváltás.'],
              'safetyNote':safety,'reviewStatus':'draft'})
    e['pose']={'schemaVersion':1,'templateId':rig['preset'],'reviewStatus':'illustrative',
               'source':'Mozdulj! szerkesztői 3D sablon; szakmai ellenőrzés szükséges',
               'frames':[],'arrows':[],'props':[],'view':'front','rig3D':rig}
    # Equipment should reflect new pose, not copied exemplar's props accidentally.
    if e['pose']['rig3D']['preset'] not in ['chair'] and not ('szék' in position.lower()):
        e['equipment']=[x for x in e['equipment'] if 'szék' not in x]
    if 'szék' in position.lower() or 'széktámasz' in name.lower():
        if 'szék' not in e['equipment']:e['equipment'].append('szék')
    if 'párna' in position.lower() or 'párnás' in name.lower():
        # No new equipment name; soft support is suggested in textual instruction, not mandatory.
        pass
    if 'pánt' in name.lower() and not any('pánt' in x for x in e['equipment']):
        e['equipment'].append('jógapánt')
    if 'jógamatrac' not in e['equipment'] and rig['preset'] in ['supine','side','tabletop','catcow','child','crosslegged','bridge']:
        e['equipment'].append('jógamatrac')
    # Keep visible 3D equipment consistent with metadata.
    for fr in rig['frames']:
        props=[q for q in fr.get('equipment',[]) if q.get('type') not in ['chair','band','dumbbell','ball','roller']]
        if 'szék' in e['equipment'] and not any(p.get('type')=='chair' for p in props):props.append({'type':'chair'})
        if 'jógamatrac' in e['equipment'] and not any(p.get('type')=='mat' for p in props):props.append({'type':'mat'})
        fr['equipment']=props
    DB['exercises'].append(e)
    added.append(eid)
DB['catalogVersion']=12
DB['editorialNote']='Szemléltető, szerkeszthető 3D mozgásábrák. Az új jógagyakorlatok önálló magyar leírással, meglévő mozgáskategóriákban; szakmai ellenőrzés szükséges.'
P.write_text(json.dumps(DB,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
report=P.parents[3] / 'docs/JOGA_BOVITES_v22.md'
report.write_text(f'''# Jógaorientált gyakorlattár – v0.22\n\n- Kiinduló gyakorlatok: 158\n- Új jóga- és légzőgyakorlatok: **{len(added)}**\n- Összes alapgyakorlat: **{len(DB['exercises'])}**\n- Nem kerültek be külön jógakategóriák, ászana-jelölők vagy jógaflag-ek.\n- Minden új elemnek van magyar leírása, biztonsági figyelmeztetése, mozgástípusa, testtáj- és edzésfázis-besorolása.\n- Minden új elemhez a 3D editor által szerkeszthető **szemléltető** Rig3D mozgásadat tartozik. Az összes póz **nem** minősül anatómiailag hitelesítettnek, és a referenciafotók részletes reprodukciója nem történt meg.\n- Az új instrukciók eredeti magyar megfogalmazású szerkesztői szövegek, nem külső szövegek szó szerinti fordításai.\n\n## Felhasznált pózjegyzékek (referencia)\n\n- https://www.yogabasics.com/practice/yoga-postures/\n- https://www.yogajournal.com/poses/library\n\n## Új elemek\n\n'''+ '\n'.join(f'- {e["name"]} (`{e["id"]}`)' for e in DB['exercises'] if e['id'] in added)+ '\n',encoding='utf-8')
print(f'Added {len(added)}, skipped {len(skipped)}, total {len(DB["exercises"])} report={report}')
