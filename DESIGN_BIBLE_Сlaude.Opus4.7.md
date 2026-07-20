# OBLAST ZERO — WORLD BIBLE & IMPLEMENTATION HOOKS

**Version 0.1 — Foundational Document**
**Project Codename:** Oblast Zero
**Engine:** Unity 6 (C#, URP)
**Genre:** Bureaucratic-Horror Survival Management Hybrid (3D Scavenge + 2D Bunker)
**Inspirations (DNA, not clones):** S.T.A.L.K.E.R., Roadside Picnic, 60 Seconds!, Darkest Dungeon, This War of Mine, Pathologic

---

## TABLE OF CONTENTS

1. Executive Summary
2. The World Bible Proper
   - 2.1 Origin Story of the Reality Distortion Field
   - 2.2 The Zone's Geography (Seven Named Oblasts)
   - 2.3 The Reality Distortion Field — Observed Effects
   - 2.4 The Emission / Blowout Phenomenon
3. Faction Engineering
   - 3.1 The Scale Society
   - 3.2 The 14th Independent Cordon Regiment ("The Cordon")
   - 3.3 The Kafedra ("The Chair")
   - 3.4 Reputation Matrix
4. Expedition Mechanics (2D Bunker Phase)
5. Anomalous Threats — New Anomalies and Mutants
6. Implementation Hooks — Unity 6 C# and JSON
   - 6.1 ScriptableObject Schemas
   - 6.2 JSON Expedition Event Payloads
   - 6.3 State Machine and Data Flow (3D → 2D → Meta)
7. Content Generation Style Guide

---

## 1. EXECUTIVE SUMMARY

**Oblast Zero** is a single-player survival management game set in an unnamed Eastern European oblast roughly twenty years after a sealed incident that the State has never officially acknowledged. Each run consists of two phases that play in alternation across a campaign of roughly fifteen to forty in-game days.

In **Phase A**, the player drops into a 3D first-person environment with a sixty-second real-time timer counting down to an atmospheric event called an Emission. They must run through a degraded apartment block, a collapsed grain depot, a flooded census office, or one of several other procedurally-seeded scavenge zones, grabbing supplies, dragging crew members, and hurling artifacts down the bunker hatch before the Emission cooks everything above ground. There is no inventory tetris. There is only physics, panic, and choice. Carry the medical kit or the second rifle. Drag the wounded crewmate or save the radio. The hatch is closing.

In **Phase B**, the survivors are sealed in a concrete bunker beneath the scavenge site. The game shifts to a 2D UI and database management view inspired by 60 Seconds! and Darkest Dungeon. Days pass. Rations dwindle. Crew members develop afflictions. The player rations water, treats radiation sickness, assigns crew to expeditions back out into the Zone, and reads expedition logs that arrive as text events with branching choices. A scavenger sent out for a tin of meat may return with a tin of meat, a new artifact, a chest wound, the conviction that the walls are breathing, or not at all. Their corpse, if recovered, is loot. Their unrecovered corpse becomes a future expedition event.

The horror is bureaucratic. The Reality Distortion Field at the heart of Oblast Zero is real, it is killing people, and no agency that exists on paper is willing to admit what it is. The player will find redacted memos, contradictory incident reports, falsified census records, and standing orders from administrative bodies that were dissolved fifteen years ago but whose patrols still operate. The horror is in the indifference of the systems. The Zone is dangerous. The paperwork is worse.

**Unique selling points.** The hybrid phase structure forces players to switch between two completely different skill sets every fifteen to thirty real-world minutes — twitch-based 3D triage decisions in Phase A, then long-form management and reading comprehension in Phase B. The text-event system is entirely data-driven from ScriptableObjects and JSON, which means content can be generated rapidly and modded easily. The faction system has no "good guys": all three factions are dangerous, all three have legitimate grievances, and all three will use the player. And the core horror — the bureaucratic obscuring of an existential threat — is a register that the survival genre has rarely committed to with this much specificity.

---

## 2. THE WORLD BIBLE PROPER

### 2.1 ORIGIN STORY OF THE REALITY DISTORTION FIELD

The official position of the State is that nothing happened.

The unofficial position, recorded across at least four mutually contradictory document trails recovered by the player over the course of the campaign, is as follows.

In late 1971, in a non-urbanized oblast in the southern industrial belt — a region whose pre-incident population had been engaged primarily in grain processing, low-grade chemical refining, and the operation of a regional Scientific-Research Bureau attached to the Academy of Sciences — a facility called **Объект 14-К (Object 14-K)** began experimental work on a class of physical phenomena that the recovered files refer to as *поле когерентного смещения* (the "coherent displacement field"). The files do not explain what this means. The files were written by people who already understood what it meant, for other people who already understood what it meant, and the explanatory documents that would have made the work legible to anyone outside the program have been redacted, destroyed in incidents officially logged as accidental flooding, or removed by personnel whose service records have themselves been redacted.

What can be reconstructed from the surviving paperwork — primarily expense reports, equipment requisition forms, and one unusually intact 1976 medical evaluation registry — is that Object 14-K was attempting to produce a stable, localized, sustained instance of a phenomenon previously observed only in transient and uncontrollable form. The medical registry records the deaths of fourteen staff members in 1973, eleven in 1974, and a single batch of forty-three in March 1975, after which the registry switches handwriting and the death rate appears to drop to zero, though the staffing levels in equipment requisitions continue to climb.

The first **Cascade Event** is dated to **22 August 1981**, based on the meteorological logs of a weather station forty-eight kilometers from the facility. The weather station observed, in the direction of Object 14-K, the simultaneous occurrence of: a sustained low-frequency atmospheric vibration audible across a thirty-kilometer radius, a localized inversion of barometric pressure for which no physical mechanism is known, and what one operator described in his shift log as "the colour going wrong over the horizon, for perhaps four minutes." The shift log was confiscated three days later by personnel identifying themselves as belonging to a special-purpose unit of the regional Civil Defense Directorate. The operator was reassigned. His subsequent service record exists in three versions, each in different archives, none of which agree on his place of death.

The State response was sealed under a 1981 directive (decree number redacted, but referred to in subsequent paperwork as "the August Provisions"). The Provisions established a containment perimeter, designated the affected territory as **Oblast Zero** for administrative purposes, and assigned its supervision to a newly created body — the **Special Resettlement and Demographic Adjustment Commission**, an organization that on paper existed to manage the relocation of the oblast's civilian population and on practice existed to make the civilian population stop existing on paper. Many of them did stop existing on paper. Fewer of them stopped existing in fact. The descendants of the people who were administratively erased but not physically removed are still in the Zone. Some of them are crew the player will recruit. None of them have valid identification documents, and the Scale Society — as the Commission is now informally called — considers this a problem to be solved.

The **Second Cascade**, the event that made the Zone permanent and that the few surviving outside scientists call the *проникновение* ("the breach"), occurred between **3 and 7 November 1991**. The dates are imprecise because the Soviet Union dissolved on 26 December of that year and a great deal of the paperwork from October and November 1991 was either destroyed in the transition, taken home by retiring officers, sold to foreign buyers in the chaotic 1992–1995 period, or in three documented cases moved into the Zone itself by people who appear to have been trying to hide it inside the anomaly. The Second Cascade produced what the surviving Bureau personnel called a "stable Field" — a region, roughly elliptical, approximately forty by sixty kilometers, inside which the laws governing matter, distance, time, and causality became negotiable. Object 14-K is somewhere inside this region. No one who has gone in looking for it has come back able to describe it consistently.

The **post-Soviet period** complicates the picture. Between 1992 and the early 2000s, at least seven different agencies — successor states' military intelligence branches, the residual Russian Federation containment apparatus, a privatized "research subsidiary" of the original Bureau that was sold to a Cypriot shell company in 1996, the newly federalized Scale Society operating under its current name from 1998 onward, two international scientific delegations whose visits were never officially acknowledged, and at least one criminal organization based in the regional capital — all attempted to assert jurisdiction over Oblast Zero. None succeeded. None withdrew. The result is the current situation: a Zone with no single legal authority, a perimeter that is sometimes a wall and sometimes a suggestion, a population of officially-nonexistent residents, and a paperwork trail so contradictory that any given document the player recovers is more likely to be a lie than the truth — and the lies, taken together, tell a story.

The **Reality Distortion Field** is what people who have to write reports call it. People who actually live in the Zone don't have a name for it. They just call it the Zone, or, when they're being precise, the *тяжесть* — "the heaviness."

### 2.2 THE ZONE'S GEOGRAPHY (SEVEN NAMED OBLASTS)

The player will not visit all seven regions in a single run. Each campaign seeds three to five of them based on the chosen scavenge site, the crew composition, and accumulated faction reputation. All regions are referenced in expedition events.

**The Outer Cordon (Внешний Кордон).** The agricultural belt that surrounds the Zone proper. Officially still farmland; in practice, a depopulated buffer of collapsing kolkhoz infrastructure, decommissioned grain silos, and the bones of livestock that wandered in during the early years. The Cordon patrols the outer edge. The Scale Society maintains a "Registration Post" at the only paved road in. This is where most scavenge runs begin and where most crew are recruited from the residual population.

**The Census District (Перепись).** A small town, pre-incident population approximately twelve thousand, current population unknown and a matter of bureaucratic dispute. The Scale Society's regional headquarters occupies the former House of Soviets. The remaining residents live in apartment blocks under a system of rationed permits. The Scale Society conducts a "census" here every spring, during which a variable number of residents are declared administratively deceased and physically removed. The town is the most reliable source of medical supplies and ammunition for the player, at the cost of significant Scale Society reputation entanglement.

**The Reservoir (Водохранилище).** A failed hydroelectric project from the 1960s, now a flooded depression containing the partially submerged remains of three pre-incident villages. The water is contaminated, but the buildings beneath it are intact and untouched. Anomalies of the "soft" gravitational class cluster here. Several artifacts are known to form only in the underwater anomaly nests. Expedition events involving the Reservoir frequently include drowning, hypothermia, and encounters with the **Drowned Census-Taker** mutant (see §5).

**The Grain Belt (Зерновой Пояс).** A region of collapsing agricultural processing plants — flour mills, oil presses, fertilizer warehouses — strung along a single defunct rail line. The Cordon maintains its main garrison here, in a fortified rail depot they call Vykhod-3. The Belt is the most heavily contested region; Cordon patrols, Scale Society "labor recovery" details, and Kafedra collection parties all operate in the same square kilometers. Crossfire events are common. The Belt is also the only reliable source of bulk food in the late campaign.

**The Bureau Quarter (Бюро).** Object 14-K is here, or was here, or never existed here, depending on which document is consulted. The administrative buildings of the original Scientific-Research Bureau still stand. They are physically intact. They are also, by every credible report, no longer at the coordinates printed in pre-incident surveys; the Quarter appears to drift, slowly, in a manner that no one has ever satisfactorily mapped. Expeditions to the Bureau Quarter have the highest sanity drain in the game and the highest payoff in pre-incident documentation, which the player can sell, trade to factions, or use to unlock specific late-campaign event branches.

**The Inner Ring (Внутреннее Кольцо).** A roughly circular zone surrounding the (notional) location of Object 14-K. The Field is at its strongest here. Time does not pass consistently. The Kafedra holds its only known permanent settlement on the Ring's edge — a converted sanitarium they call **Пансионат** ("the boarding house"). Expeditions into the Ring frequently return crew who have aged differently than the days they were away, who arrive a week before they left, or who do not arrive at all but whose voices can be heard, faintly, on the bunker's salvaged radio for several days afterward.

**The Threshold (Порог).** The interior. The center. No one who has gone in has come back, except for three people, all of whom were members of the Kafedra and none of whom were physically the same when they returned. The Threshold is not visited in expeditions; it exists in the lore as a destination, a rumor, and a late-game event hook for runs that meet specific narrative prerequisites.

### 2.3 THE REALITY DISTORTION FIELD — OBSERVED EFFECTS

The game's design discipline is that the Field is *never* fully explained. Every in-fiction document that purports to explain it contradicts every other in-fiction document. What the player encounters are *effects*, and the effects are the horror.

Recorded effects include: localized inversion of cause and effect (objects observed to have been broken before they were dropped); duplication of biological organisms, including but not limited to crew members, with the duplicates frequently retaining partial memories of the original and frequently being indistinguishable from the original by any available test; spatial recursion (corridors that loop; rooms that contain themselves); temporal pooling (regions where elapsed time accumulates as a physical pressure, manifesting as fatigue, accelerated aging, or what the recovered medical literature calls "chronic chronological exhaustion"); and the consistent failure of certain categories of recording equipment, particularly photographic film and analog magnetic tape, to capture what was observed by the operator's unmediated senses. Digital media fails differently and less predictably.

The Field is *thicker* in some regions than others. It pulses. The Emission events that bookend Phase A are the most violent and most regular of these pulses, occurring across the entire Zone simultaneously and lasting approximately three to six in-game minutes. Between Emissions, the Field's local intensity varies. Some expeditions will succeed because the Field was thin that day. Some will fail because it was not.

### 2.4 THE EMISSION / BLOWOUT PHENOMENON

Stalkers call it the *выброс* — the Emission. Cordon officers call it Atmospheric Event Class C. The Scale Society's standing protocols call it "Scheduled Field Activity" and treat its civilian victims as foreseeable administrative wastage. The Kafedra calls it *дыхание* — "the breathing" — and considers it a sacrament.

Whatever it is, it kills everything above ground. The mechanism is unclear; survivors who have been partially exposed report a sensation of "being read," followed by neurological symptoms ranging from temporary aphasia to total dissolution of personality. Bodies recovered after Emission exposure are physically intact and biologically dead. Some of them stand up several hours later and walk, slowly, in a direction that nobody can ever subsequently identify by reference to the geography.

**Phase A is the sixty seconds before an Emission.** The player's character has just received warning — typically via radio crackle, sometimes via a Cordon siren, occasionally via the spontaneous failure of the Field's "thin" pockets nearby — and has exactly that much time to grab what they can and reach the bunker. The Emission ends Phase A whether the player has retrieved what they wanted or not. Anything still above ground is lost.

There is no second chance to revisit the scavenge site. Each Phase A is a one-shot.

---

## 3. FACTION ENGINEERING

The three factions exist in a three-way deadlock. None of them can eliminate the others. All of them want different things from the Zone and from the player. The player will, over the course of any given run, end up cooperating with at least one of them and probably antagonizing the other two. There is no "good" faction. There are three flavors of dangerous.

### 3.1 THE SCALE SOCIETY (Общество Весов)

**Internal nickname.** "The Clerks." Stalkers and crew members use this dismissively. Among themselves, Scale Society personnel refer to one another by title and registration number only.

**Founding event.** Established 1981 as the Special Resettlement and Demographic Adjustment Commission, an emergency civil-defense body created under the August Provisions to "manage the orderly transition" of Oblast Zero's pre-incident population. Re-federalized in 1998 under its current name as a "non-departmental administrative entity," which is bureaucratic language meaning that no government will admit it reports to them, but every government cashes its quarterly reports.

**Current leadership.** Senior Coordinator **Eleonora Vyacheslavovna Surikova**, age estimated mid-sixties, has held her current post since 2007. She has never been photographed. Her signature appears on every Scale Society document the player will recover. She is referenced in three Kafedra prophecies, in which she is variously the bride, the auditor, and the door. The Cordon has a standing kill order on her dated 1994, which she is aware of and which she has never been observed to take seriously.

**Ideology.** The Scale Society believes — and the player will find this argued, in their own documents, with the chilling reasonableness of people who genuinely think they are the only adults in the room — that the Zone is a *resource*. Specifically, it is a resource for adjusting populations. The Society's stated mandate is "demographic stabilization," and in practice this means the management of a controlled flow of administratively-undesirable people into the Zone, where they are deployed as labor for artifact recovery, as test subjects for "field tolerance" studies, and, when neither use applies, as ration-cost reductions. The Society's internal calculus is actuarial. People are line items. Population pressures in the surrounding regions are managed by absorbing the surplus.

**End goal.** To maintain the current equilibrium indefinitely. The Society does not want the Zone closed. They do not want it expanded. They want it *administered*. They are, in the most literal sense, the only faction that wants the situation to continue as it is.

**Recruitment.** Internal promotion only. New Society personnel are drawn from the regional civil service via opaque selection processes. Cordon defectors are sometimes accepted. Civilians never are. Crew members the player recruits may have *survived* the Society, but cannot be ex-Society.

**Attitude toward the player.** Initially neutral. The Society will offer the player contracts — recover document X, eliminate person Y, deliver artifact Z to drop point Q — and pay reliably in food, medical supplies, and information. Player characters who accept enough contracts become "approved contractors" and gain access to Census District trading. Player characters who refuse, or who interfere with Society operations, are reclassified as "demographic anomalies" and become subject to elimination, which in the Society's procedure manual is a five-step administrative process with a defined paper trail.

**Attitude toward the Cordon.** Hostile but functional. The Society considers the Cordon a "regrettable jurisdictional irregularity" that should have been retired in 1993. The Cordon considers the Society to be war criminals operating under bureaucratic cover. They mostly avoid each other. When they do not avoid each other, the Society loses, because the Cordon has the heavier weapons. The Society compensates by maintaining a list of every Cordon officer's pre-incident relatives, addresses, and outstanding civil debts.

**Attitude toward the Kafedra.** Active hostility. The Society regards the Kafedra as "biological waste with delusions of agency." Kafedra members who are captured by the Society are processed for "tissue resources." The Kafedra is the only faction the Society has standing orders to engage on sight.

**Signature equipment / aesthetic.** Long grey wool coats over civilian clothing. Clipboards. Manila folders. Stamp seals carried on lanyards. Sidearms are issued but rarely visible; Society field personnel prefer to have problems resolved by Cordon defectors on retainer or by hired Loners. Their vehicles are pre-incident GAZ utility sedans, maintained obsessively, repainted in a specific shade of office-furniture grey that the Society calls "Reference Grey 14."

**Sample radio chatter.**
- "Registration confirmed. Subject is now line item. Proceed."
- "We have a Section 6 deviation in grid four. Send a senior clerk and two contractors. No firearms in evidence, please."
- "If they have papers, the papers are forged. We did not issue them. Note the discrepancy and proceed with re-registration."

**What happens if the player aligns with them.** The Census District opens for trade. The Society begins assigning the player long-term contracts that pay well in materials but increasingly require the player to carry out demographic adjustments — i.e., the targeted killing or removal of named individuals in the Zone, some of whom the player will have met or recruited. The endgame for full Society alignment is the "Stabilization" ending, in which the player's bunker is incorporated as a Section 12 Provisional Field Office, the player is registered as a regional coordinator, and the game ends with the player signing their first quarterly demographic adjustment quota.

### 3.2 THE 14TH INDEPENDENT CORDON REGIMENT ("THE CORDON")

**Internal nickname.** "Кордон" — "the Cordon." Outside it, they are called "the Lost Regiment," "the August Boys" (a reference to the August Provisions), or simply "the green ones," after their pre-incident uniform color, which is the only color of fabric they still have access to and which is now faded to a uniform pale grey.

**Founding event.** The 14th Independent Regiment of Civil Defense Special-Purpose Troops was deployed to the Oblast Zero perimeter in September 1981 under the August Provisions. Their orders, on paper, were to maintain the containment cordon for "a period of no less than ninety days, pending further instruction." Further instruction never arrived. The Regiment's parent command structure was reorganized in 1985, reorganized again in 1989, dissolved in 1992, briefly reconstituted under a different name in 1995, and then dissolved permanently in 1998. At each reorganization, the 14th's standing orders were "preserved pending review." The review never occurred. The Regiment is still on the perimeter. The Regiment is still following its 1981 orders.

**Current leadership.** Colonel **Pyotr Ignatevich Vereshchagin**, age sixty-eight, succeeded his predecessor — a man he refers to only as "the previous Colonel" — in 2003. He has not left the Cordon's main garrison at Vykhod-3 since 2009. He communicates with field patrols by handwritten dispatch carried by runners, on the stated grounds that he no longer trusts radio. The Colonel has three subordinate officers, all of whom were born in the Zone to Cordon parents and have never lived outside it. The Cordon's total active strength is approximately ninety personnel, down from a 1981 establishment of just over four hundred.

**Ideology.** The Regiment believes its orders are still valid. The 1981 orders direct it to: maintain the cordon; prevent unauthorized ingress and egress; detain and turn over to "the appropriate civil authority" any persons found inside the cordoned area; and "interdict any phenomena threatening to breach containment." The Regiment has interpreted the term *appropriate civil authority* with increasing creativity over the decades — at various points it has meant the regional Party committee (now defunct), the Federal Border Service (which does not acknowledge the Cordon's existence), the Scale Society (briefly, between 1998 and 2003, until the Colonel concluded they were "the wrong kind of civilians"), and now means "no one," meaning the Regiment detains people indefinitely. They are not cruel. They are not arbitrary. They are following orders that no one will rescind because the bureaucracy that issued them no longer exists.

**End goal.** Compliance. They are waiting for relief. They have been waiting for forty-five years. They will continue waiting. Some of them know, privately, that no relief is coming. They will not say so out loud, because to say so out loud would be desertion.

**Recruitment.** The Cordon is genetically and demographically closed. New personnel are born to existing personnel. They have a stable population of approximately ninety. The garrison at Vykhod-3 includes a school, a clinic, a small farm, and a chapel that has been deconsecrated three times by different chaplains and which the current officers describe as "non-religious infrastructure." Defectors leave occasionally; the Cordon's standing order on defectors is to shoot on sight, but in practice they let them go, because shooting them would require the patrol officer to write a report, and the report would be reviewed by the Colonel, and the Colonel reads everything.

**Attitude toward the player.** Reflexively hostile. The Cordon's standing orders treat any person inside the cordoned area as an unauthorized civilian to be detained, and any person attempting to leave as a containment risk to be shot. In practice, the player can negotiate with Cordon patrols by demonstrating the right documents (which are forged by the Kafedra and trade for high prices), by carrying intelligence the Cordon wants (Scale Society movement patterns, Kafedra ritual sites), or by killing Scale Society personnel in front of them, which the Cordon regards as a freelance contribution to interdiction operations.

**Attitude toward the Scale Society.** Loathing. The Cordon's institutional memory includes the 1993 incident in which a Scale Society predecessor body attempted to assert authority over the Vykhod-3 garrison and was repulsed at a cost of fourteen Cordon dead and an unknown but larger Society number. The Cordon has never forgiven this. The Colonel keeps a list.

**Attitude toward the Kafedra.** The Cordon considers the Kafedra "containment failures" — people who have already been compromised by the Zone and who therefore exist outside the cordon's purview. The Cordon does not generally engage Kafedra members because they regard them as "no longer the kind of thing the Regiment is here to shoot." This is a delicate accommodation. The Kafedra reciprocates by leaving Cordon patrols alone, mostly.

**Signature equipment / aesthetic.** AKM-pattern rifles, maintained meticulously, with stocks that have been replaced and re-replaced from salvaged wood. Pre-1989 Soviet equipment exclusively; the Cordon refuses to incorporate any post-1991 materiel on the grounds that its provenance is unverified. Hand-stitched uniform repairs. Heavy use of paper documentation: every patrol carries a logbook, every encounter is recorded, every shot fired requires a written justification submitted to the Colonel within seventy-two hours.

**Sample radio chatter.** They prefer not to use radio. When they do:
- "Pattern Six-One. Two civilians, papers irregular. Holding for interrogation."
- "Vykhod-3, Patrol Yelena. Engaging Scale element. Documentation will follow."
- "Colonel sends regards. Patrol returning. Casualties: one. Name to follow in dispatch."

**What happens if the player aligns with them.** The Cordon will eventually grant the player "auxiliary" status, which involves issuing the player a 1980s service rifle, a packet of forged identification, and a copy of the standing orders. The player is then expected to support patrol operations. The endgame for full Cordon alignment is the "Relief" ending, in which the player's bunker becomes a forward operating post for what the Colonel describes as "the long-awaited consolidation of the cordon," and the game ends with the Colonel issuing the player a battlefield commission and the first piece of orders he has personally drafted since 2009.

### 3.3 THE KAFEDRA — "THE CHAIR" (Кафедра)

**Internal nickname.** "Кафедра" — literally "the chair" or "the academic department," a wordplay on the institutional meaning. Outsiders call them the Boarding House, the Modified, or — among the most superstitious Loners — *the Listeners*.

**Founding event.** The Kafedra emerged between 1992 and 1996 from the remnants of three groups: the residual scientific staff of Object 14-K and the associated Bureau (who had nowhere to go after the Soviet collapse and could not safely leave the Zone they had helped create); a faction of Bureau-adjacent researchers who had been conducting unsanctioned biological experiments on themselves since the early 1980s in pursuit of "field tolerance"; and a number of pre-incident civilians who had survived inside the Zone by means that no one is willing to discuss. The unifying figure of this convergence was **Dr. Mikhail Arsenyevich Vinogradov**, a senior researcher who in 1989 published a single unauthorized samizdat paper arguing that the Field was not a physical phenomenon but a *cognitive* one, and that adaptation required modification of the observer.

**Current leadership.** Vinogradov is approximately one hundred and four years old, if he is still alive, which is contested. He has been formally succeeded three times by candidates referred to as "the Chair" (hence the faction's name). The current Chair is a woman known only as **Lidiya**, who is not original to the Kafedra — she was a Cordon medic who defected in 2014 — and who is undergoing the late stages of the Kafedra's biological modification regimen. By the player's encounters with her in the late campaign, Lidiya is no longer reliably a single biological entity. Her speeches use the first-person plural and the first-person singular interchangeably.

**Ideology.** The Field is not an enemy. The Field is the most important thing that has ever happened. The Kafedra holds that ordinary human cognition is *incompatible* with the regions where the Field is strong, and that survival inside those regions — let alone meaningful contact with whatever the Field is — requires deliberate biological adaptation. They have been adapting themselves since the early 1980s. The methods vary. Some are surgical (grafts, implants, the deliberate cultivation of "responsive tissue" derived from anomaly-exposed organic material). Some are chemical (long-term hormonal regimens, drugs derived from artifacts, infusions of fluid extracted from the bodies of certain mutants). Some are environmental (extended residence in specific regions of the Field where the modifications "take" better). The result is a faction whose senior members are no longer entirely human in any sense the Cordon's medical officers or the Scale Society's actuaries would recognize, and which considers this a *step forward*.

**End goal.** *Crossing*. The Kafedra's stated objective is to produce, through sustained adaptation across generations, a successor population capable of inhabiting the Threshold — the interior of the Zone — and making contact with whatever is there. They do not know what is there. They believe, with the precision of faith, that what is there is *worth meeting*.

**Recruitment.** The Kafedra accepts defectors, refugees, terminal patients, the curious, and the desperate. They are not predatory in their recruiting; the modification process is described to candidates accurately, including its mortality rate (variously reported as between forty and eighty percent). Crew members the player has recruited may *become* Kafedra after sustained Zone exposure produces afflictions the Kafedra knows how to manage; the player can lose crew this way without losing them to death.

**Attitude toward the player.** Patient. The Kafedra is the only faction that genuinely does not want anything from the player except, occasionally, the player. They will offer trade — artifact identification, medical knowledge, modified equipment — and they will offer recruitment, which is presented as an invitation rather than a demand. Players who repeatedly damage Kafedra operations will be warned, then ignored, then eventually targeted by individual Kafedra senior members operating without sanction. The Kafedra does not maintain a hostility doctrine. Individual members maintain grudges.

**Attitude toward the Scale Society.** Active hatred. The Kafedra understands, more clearly than any other faction, what the Scale Society does to its raw material. They will go out of their way to disrupt Society operations. They will not, however, attack Society personnel directly unless cornered, because the Society's response capacity is documented.

**Attitude toward the Cordon.** Cautious accommodation. The Kafedra understands that the Cordon is, in its own way, also a survival adaptation — an entire community that has refused to acknowledge that the world they were defending no longer exists. The Kafedra finds this poignant. They have, on multiple occasions, provided unsolicited medical assistance to Cordon personnel who were stranded outside their patrol radius. The Cordon does not officially accept this and has never officially returned the favor, but the Colonel has been known to allow Kafedra collection parties to operate near Vykhod-3 without interdiction.

**Signature equipment / aesthetic.** Heavily personalized clothing. Surgical aprons over civilian sweaters. Visible scars and grafts. Senior members wear masks of carved wood, leather, or — in two confirmed cases — bone. Equipment is hand-modified, often grotesquely; weapons incorporate organic components, lenses are ground from artifact fragments, medical kits include syringes that pulse faintly when the bearer is breathing. Their permanent settlement, the Pansionat, is a former Soviet sanitarium with the windows replaced by stretched membranes that the senior members describe as "more breathable than glass."

**Sample chatter.** The Kafedra does not use radio. They communicate with one another by means that include, but are not limited to, written notes left under specific rocks, low whistles in particular intervals, and — for senior members — what observers describe as "looking at one another for an extended period." Their published material consists of pamphlets, hand-copied and distributed via dead drops, in which the prose has the precision of academic writing and the content of religious devotion.
- (From pamphlet 6.) "The error of the State was to suppose that the Field was an *event*. The Field is a *register*. We have been writing on the wrong page."
- (Whispered, by a Kafedra collection officer who has identified the player at a distance.) "Carry on. We see you. We will see you again."

**What happens if the player aligns with them.** The Kafedra will eventually offer the player the modification regimen. Acceptance triggers a sequence of bunker-phase events in which the player character undergoes the early stages of modification — first benefits, then complications, then permanent changes — culminating in the "Adaptation" ending, in which the player abandons the bunker entirely and walks, with surviving crew who have accepted the same regimen, toward the Threshold. The screen does not cut to black. The screen cuts to a long, slow zoom on the open door of the bunker, the player's perspective receding, the last frame held for several seconds before the credits.

### 3.4 REPUTATION MATRIX

The three factions maintain a closed reputation system. Actions that help one faction always harm at least one other, and there is no "neutral" path that allows the player to be friendly with all three. The matrix is:

| Player Action                                                | Scale Society | Cordon | Kafedra |
|--------------------------------------------------------------|---------------|--------|---------|
| Complete a Scale contract                                    | +15           | −5     | −10     |
| Kill a Scale field clerk                                     | −40           | +10    | +20     |
| Hand over a Cordon defector to the Society                   | +25           | −50    | −15     |
| Resupply a stranded Cordon patrol                            | −10           | +20    | 0       |
| Kill a Cordon officer                                        | +15           | −50    | 0       |
| Deliver an artifact to the Kafedra                           | −10           | 0      | +20     |
| Refuse the Kafedra modification offer                        | 0             | +5     | −5      |
| Accept the Kafedra modification offer                        | −15           | −10    | +35     |
| Recover and return a Cordon dispatch lost during an Emission | 0             | +30    | 0       |
| Recover and turn in pre-incident Bureau documents to Society | +20           | 0      | −20     |
| Recover and turn in pre-incident Bureau documents to Kafedra | −15           | 0      | +25     |
| Burn pre-incident Bureau documents                           | −5            | +5     | −20     |

Reputation thresholds gate event branches, vendor access, and faction-specific endings. Going below −60 with any faction unlocks "hunted" status, in which dedicated hostile events begin appearing in expedition logs. Going above +60 with any faction unlocks that faction's endgame branch. The player can be in good standing with at most one faction at a time after day fifteen.

---

## 4. EXPEDITION MECHANICS (2D BUNKER PHASE)

This section describes the mechanical heart of Phase B. The bunker phase plays in a loop of *days*, each of which advances by player choice. On each day the player resolves: a morning intake (consume rations, apply ongoing afflictions, resolve queued events from the previous night), an action window (assign crew to tasks, including expeditions), and a night intake (resolve expeditions in progress, generate new events).

**Expedition flow.** When the player assigns a crew member to an expedition, the player chooses: the destination region (one of the seven described in §2.2, gated by current map knowledge), the expedition duration (one, two, three, or five days), the loadout (any items the bunker can spare, including weapons, food, water, medical supplies, and protective gear, subject to the crew member's carry weight), and the intent (scavenge, scout, trade with a specific faction, deliver a payload, or recover a body). The expedition then resolves *off-screen* across the chosen duration. The player does not control the expedition turn by turn. The player receives, on each day of the expedition, one or more text events with branching choices.

**The carry-weight loop.** Each crew member has a baseline carry capacity (default 25.0 kg, modified by their background — ex-Cordon are heavier, ex-Kafedra are lighter but more efficient with awkward loads, Loner scavengers have higher capacity but heavier sanity penalties for overloading). The expedition's outgoing load is set at assignment. Heavier outgoing loads slow the expedition, increasing encounter probability per day and reducing the maximum incoming load. Heavier *return* loads — the loot the crew member is bringing back — make them slower on the return leg and dramatically increase encounter probability on the last day. Players quickly learn that the optimal expedition involves sending the crew member out lean and bringing them back full, but bringing them back full means they're slow, and being slow is when bad things happen.

**Sanity and the Darkest-Dungeon affliction loop.** Every crew member has a Sanity stat on a 0–100 scale, starting at 100. Sanity drains during expeditions from a baseline source (the Field's pervasive psychological pressure) and from event-specific sources (witnessing a mutant, encountering specific anomalies, losing a comrade, performing acts the crew member's background flags as morally costly). When Sanity drops below 40, the crew member enters a **stressed** state and event outcomes begin to skew negative. When Sanity drops below 20, the crew member rolls on an **Affliction Table** and acquires a permanent trait. Afflictions include:

- *Paranoid* — refuses to expedition with another specific crew member; first encounter per expedition resolves at worst outcome.
- *Hollow* — −20 sanity recovery rate; gives away rations without consent.
- *Compromised* — passive radiation contamination; bunker radiation pool rises 1 per day per Compromised crew.
- *Listening* — whispers to objects; sometimes returns from expeditions with items the player did not authorize them to carry.
- *Splintered* — carry weight halved permanently; refuses to carry firearms.
- *Witnessed* — has seen the Threshold from a distance; +30 sanity recovery in the bunker, −30 in any expedition that enters the Inner Ring.

Conversely, a small number of crew members exposed to specific high-value events at *high* Sanity will roll on a **Virtue Table** and acquire a positive trait — *Steady*, *Observant*, *Grim*, *Read* — each of which provides expedition bonuses.

**Radiation.** Each region has a baseline radiation level. Each crew member accumulates personal radiation across expeditions, reduced by protective gear, increased by exposure events. Personal radiation above 300 units triggers Radiation Sickness, an ongoing affliction that reduces all stats and increases mortality on any expedition. Personal radiation above 600 is fatal within two to four days regardless of treatment. The bunker has a baseline radiation pool that increases when contaminated items are stored without lead shielding; this pool affects all crew passively.

**Permadeath and body recovery.** When a crew member dies on expedition, their gear and accumulated loot are lost *unless* a subsequent recovery expedition is sent to the death site within four in-game days. Recovery expeditions are dangerous and emotionally costly (high sanity drain on the recovery crew member), but they return both the lost gear and the body, which can be: buried in the bunker's growing memorial wall (small permanent morale bonus to all remaining crew); cremated (no bonus, no penalty, the body is gone); donated to the Kafedra (significant Kafedra reputation, mild morale penalty); turned in to the Scale Society (Society reputation, moderate morale penalty, the body is "processed"); or kept in the bunker's cold storage (gradual ongoing sanity drain on all crew, until disposed of). If no recovery is mounted, the body becomes a future expedition event hook — three to seven days later, an event may surface in which another scavenger has found the body, looted some of it, and the player must decide how to respond to the looter.

**Crew specializations.** Each crew member has a primary background drawn from: **Loner Scavenger** (high carry, high encounter resistance, moderate sanity), **Ex-Cordon Soldier** (high combat resolution, high baseline sanity, low artifact identification), **Ex-Society Clerk** (high trade outcomes, high paperwork-event resolution, severe penalties in Kafedra-aligned events), **Field Medic** (allows in-expedition healing, can stabilize wounded crew), **Mechanic** (allows in-bunker repair of degraded equipment, generates passive ration of crafted goods), **Kafedra Defector** (high anomaly resistance, can identify artifacts in the field, severe Scale Society penalties), and **Ecologist Survivor** (a former research-station resident; high knowledge of region geography, balanced stats, signature ability to read pre-incident documents). The crew composition the player ends a campaign with — and the crew composition they brought into Phase A in the first place — heavily influences which events the system can generate.

**The day-advance algorithm.** At the end of each player-confirmed day, the system:
1. Decrements all rations consumed (per-crew, modified by afflictions).
2. Decrements water and applies dehydration where insufficient.
3. Applies bunker radiation pool damage.
4. Resolves ongoing afflictions (Radiation Sickness deals damage; *Hollow* removes a ration; etc.).
5. For each crew member on expedition, advances expedition state by one day, rolling encounter probability, generating event(s), and queuing them for player resolution.
6. For crew in the bunker, applies sanity recovery (modified by traits and bunker conditions).
7. Resolves morale ticks.
8. Generates the next morning's queued events (faction visits, radio broadcasts, structural failures, unsolicited visitors).
9. Saves the run.

The full design discipline is that *nothing in the bunker phase is hidden information from the player*. Every stat, every probability, every modifier should be inspectable. The horror is not in obfuscated mechanics; the horror is in the player making an informed choice and watching it kill someone.

---

## 5. ANOMALOUS THREATS — NEW ANOMALIES AND MUTANTS

Three original anomalies and two original mutants. Each is documented as an in-fiction redacted record, followed by mechanical specification, followed by example expedition-log prose.

### 5.1 ANOMALY: THE CARBON COPY (Углеродная Копия)

**Classification code.** ANM-Δ-07/CC.
**Field name.** "The copy," "the carbon," "the desk drawer."

**Redacted incident report excerpt (1987).**
> *Personnel of Survey Team [REDACTED] reported, on [REDACTED, est. April 1987], the apparent recovery of three duplicate copies of [REDACTED] internal memo No. 14-K/[REDACTED], dated 11.iv.1987. The duplicates were physically identical to the original in every respect including ink composition, paper aging, and the [REDACTED] of the [REDACTED]. Each duplicate, however, bore minor textual variation. Variations included: alteration of named personnel from "Tov. [REDACTED]" to "Tov. [REDACTED]"; alteration of stated incident date by between four and eleven days; and, in one (1) instance, the inclusion of a final paragraph instructing the reader to "destroy upon reading," which paragraph was absent from the original. Survey Team [REDACTED] is recorded as having destroyed all four (4) documents in compliance with this instruction. The instruction was not authorized.*
> *Survey Team [REDACTED] is currently classified as [REDACTED].*

**Mechanical behavior (Phase A).** The Carbon Copy is an environmental anomaly that occupies a small volume (roughly two cubic meters) and is *invisible until interacted with*. The player will encounter a Carbon Copy by attempting to pick up an item — a tin of food, a med kit, a document — within the anomaly's volume. The item picks up correctly. As soon as the player turns to move on, the Copy generates a *duplicate* of that item, in the same position, indistinguishable from the original. If the player picks up the duplicate, the Copy generates another. Time pressure being what it is, players in Phase A who encounter a Copy frequently grab three or four "copies" of the same item, dump them in the bunker, and discover in Phase B that only one of the copies is the original — the others, on inspection, contain subtle errors. A tin of meat that contains the wrong cyrillic on its label. A med kit whose contents are correct but whose syringes inject a clear fluid that is not what it should be. A document signed by someone who could not have signed it.

**Mechanical behavior (Phase B / expedition log).** Carbon Copies are referenced in expedition events. An expedition crew member who reports recovering a duplicate of a previously-known document is providing information the player must decide how to trust.

**Loot.** The Copy "produces" duplicates of items the player brings into it. There is no direct loot drop. However, certain artifacts known as **Margin Notes** form spontaneously inside Carbon Copy anomalies that have been undisturbed for long periods. A Margin Note is a small object — typically resembling a folded page — that, when carried, allows the player to *re-roll* one expedition event outcome per in-game week.

**Counter-tactics.** The Copy is detectable by the geiger counter (it produces a characteristic non-radioactive "double-click" pattern), and by a careful player who notices that an item is in a position it was not before.

**Expedition log prose example.**
> "Marina says she found the medical cache I had marked on the map. She says she found it twice. She is not sure which of the two crates she brought back, or whether she brought back both, or whether she brought back neither. The crate on the table looks correct. The crate on the floor also looks correct. Marina has not slept."

### 5.2 ANOMALY: THE INTERVIEW (Собеседование)

**Classification code.** ANM-Ψ-12/IV.
**Field name.** "The interview," "the questionnaire," "the office," "the long room."

**Redacted incident report excerpt (1989).**
> *On [REDACTED], Cordon Patrol [REDACTED] reported the disappearance of Pvt. [REDACTED] during a routine sweep of the abandoned Census District Registration Bureau. Pvt. [REDACTED] entered Room 14 of the Bureau at approximately 14:20. At approximately 17:50, after Pvt. [REDACTED] failed to emerge, the patrol commander entered Room 14 and observed the room to be empty. The room measured approximately 4m × 6m and contained no furniture. The patrol withdrew. At approximately 21:00, Pvt. [REDACTED] was observed walking from the building, carrying a stamped and signed form bearing his name, his service number, and a section of typewritten text purporting to be the transcript of an interview Pvt. [REDACTED] had reportedly undergone over the preceding six hours and forty minutes. Pvt. [REDACTED] was unable to recall the interview. Pvt. [REDACTED] was unable to recall the previous seven (7) years of his life with consistent reliability for the following [REDACTED] days. The transcript referenced personnel and events that [REDACTED]. Pvt. [REDACTED] was subsequently [REDACTED].*

**Mechanical behavior (Phase A).** The Interview is a room-scale anomaly that only manifests in certain interior spaces — administrative offices, classrooms, the back rooms of factory canteens, the Bureau Quarter. A room contains an Interview if, upon entry, the room's interior is *larger* than its exterior would suggest, by a factor that varies with Field intensity. Inside, the room is empty except for a single desk, a chair, and a stack of forms. The player can walk past the desk safely. The player can also *sit down*. Sitting down triggers a sequence in which the screen fades to black, the timer pauses, and the player is presented with a series of text prompts representing an interview being conducted by an unseen interlocutor. The questions are mundane (name, service number, prior employment); the *follow-up* questions are not. Completing the interview returns the player to the room with a piece of paperwork — and a permanent or semi-permanent buff or debuff, depending on how they answered. Refusing to sit, or attempting to leave during the interview, is safe but forfeits the reward.

**Mechanical behavior (Phase B).** Crew members may be assigned to "visit the Interview" as a specific late-campaign expedition type. Outcomes range from acquiring a Margin Note, to having a permanent affliction lifted, to acquiring a new permanent trait, to never returning, to returning as someone who is not the crew member who left.

**Loot.** The Interview is the only known reliable source of two specific artifacts: the **Notarized Heart** (reduces personal radiation accumulation by 50%) and the **Stamped Tongue** (allows a one-time "official override" of any Scale Society event, with consequences).

**Counter-tactics.** Do not enter rooms that feel too large. The Cordon has, over the decades, written the locations of confirmed Interview rooms onto trading maps that the player can recover — but the rooms are not stationary, and the maps are now incorrect for an unknown but significant percentage of marked sites.

**Expedition log prose example.**
> "Yefim came back. He brought a form. The form has his name on it. The form has my name on it as well, in his handwriting, in the box marked 'next of kin.' I am not his next of kin. I do not know where he learned my middle name."

### 5.3 ANOMALY: THE BACKLOG (Долговой Слой)

**Classification code.** ANM-Χ-21/BL.
**Field name.** "The backlog," "the wait," "the queue."

**Redacted incident report excerpt (2003).**
> *Recovery teams entering the southern wing of the former Registration Bureau on [REDACTED] reported encountering a localized region in which temporal progression deviated from the standard reference frame. Personnel entering the region observed external personnel to be moving at substantially elevated speed; personnel external to the region observed those inside to be effectively motionless. Three (3) recovery team members entered the region. Two (2) emerged approximately seventy-three (73) seconds later by external reference; they reported that they had been inside the region for an interval they each estimated at between fourteen and seventeen hours. The third member [REDACTED]. The third member emerged from the region on [REDACTED, est. 2009, six years subsequent to entry]. He was not visibly aged. He was carrying a paper bag containing his lunch, which he reported as still warm. He was unable to be debriefed.*

**Mechanical behavior (Phase A).** The Backlog is a volumetric time-distortion anomaly. Inside it, subjective time runs slower than external time — typically by a factor of forty to one hundred, occasionally higher. In Phase A, encountering a Backlog appears, externally, as a region the player can step into that visibly *slows them down*: the timer keeps running, but the player's movement and interaction speed drop to a crawl. Players who step into a Backlog with thirty seconds left on the timer have effectively forfeited their run. The Backlog is, however, visible — a region of subtly distorted air, with dust motes that hang motionless. Skilled players can identify and avoid Backlogs. Greedy players who think they can grab one more item from a shelf inside a Backlog discover that they cannot.

**Mechanical behavior (Phase B).** Crew members trapped in a Backlog during an expedition return *late* — often days after they were expected. The player must continue running the bunker without them. When they return, they have not aged the same number of days the bunker has aged. The discrepancy generates events: the returning crew member does not recognize new bunker arrivals; the bunker has used or rationed items the crew member expected to find; relationships in the bunker have shifted.

**Loot.** Backlogs themselves do not produce artifacts. However, items left inside a Backlog for an extended external interval can sometimes *change* — perishables emerge fresh decades later; documents emerge with their ink still wet; in three recorded cases, items emerged with content that had not been written when they were placed inside.

**Counter-tactics.** Visual identification (slow dust). Throwing a small object in (if it slows visibly, the volume is anomalous). Refusing to enter under time pressure.

**Expedition log prose example.**
> "Olga left for Vykhod-3 on day twelve. It is day twenty-one. She has not returned. The note she left at the Backlog edge, in her own handwriting, says she will be back on day fifteen. The note is dated tomorrow."

### 5.4 MUTANT: THE DROWNED CENSUS-TAKER (Утопший Переписчик)

**Classification code.** MTN-Β-04/DC.
**Field name.** "The drowned man," "the wet clerk," "переписчик."

**Redacted incident report excerpt (1998).**
> *Following the partial inundation of the [REDACTED] settlement during the November 1997 reservoir overflow, recovery operations recovered a total of seventy-three (73) human remains, of which sixty-one (61) were identified and twelve (12) were retained for further study. Subsequent observation of the retained specimens indicated that an undetermined number had retained a degree of motor function inconsistent with the post-mortem interval. Specimen [REDACTED] was observed, on [REDACTED], to have left the secured holding facility. Specimen [REDACTED] was subsequently recovered on [REDACTED] in the office of the [REDACTED] Registration Bureau, where it had apparently been engaged in the activity of [REDACTED] for an interval estimated at no less than [REDACTED] days. The forms it had been [REDACTED] were [REDACTED] and bore signatures.*

**Mechanical behavior (Phase A).** The Drowned Census-Taker is a slow-moving humanoid mutant, formerly a Scale Society census enforcer or registration clerk, killed in or near water in the Zone (most commonly in the Reservoir) and reanimated by the Field. They move at approximately walking pace. They carry, almost universally, a waterlogged clipboard and a fountain pen. They do not attack the player directly. They *follow* the player. They take notes. If the player stops moving for more than approximately ten seconds within line of sight of a Drowned Census-Taker, the Census-Taker will catch up, raise its clipboard, and begin *writing the player's name*. Once this process completes — approximately fifteen seconds — the player suffers a permanent stat penalty for the remainder of the run: they have been "registered." Multiple registrations stack. The penalty applies in both Phase A and Phase B.

**Mechanical behavior (Phase B).** Drowned Census-Takers appear in Reservoir expeditions, Census District expeditions, and any expedition that involves crossing standing water. They are sanity-drain events first and combat events second. Crew members who report a "wet clerk" sighting take a small permanent Sanity hit. Crew members who are caught and registered acquire the *Compromised* affliction.

**Loot.** The clipboard is loot. Recovered clipboards from Drowned Census-Takers contain registration forms partially filled out for various Zone inhabitants, including, in some cases, the player's own crew. The forms can be: turned in to the Scale Society for reputation; burned for a small morale boost; or read carefully, in which case the player may learn an in-fiction name or identifier for a current crew member that the crew member has never disclosed, which can unlock special dialogue and events.

**Counter-tactics.** Movement. Keep moving. Do not stop. Firearms work but make a great deal of noise, which in the Reservoir attracts other Drowned. Edged weapons are quieter but require proximity, and proximity is when the clipboard rises. The most reliable counter is to never let one get within ten meters in the first place.

**Expedition log prose example.**
> "Pavel reports a sighting near the eastern overflow. He says it was wearing a Scale coat. He says it was writing as he ran. He says he heard it speak — quietly, conversationally, as if confirming a spelling — and what it said was the name of his mother."

### 5.5 MUTANT: THE EDITOR (Редактор)

**Classification code.** MTN-Ψ-09/ED.
**Field name.** "The editor," "the corrector," "правка" (a Russian noun meaning "an edit" or "a correction").

**Redacted incident report excerpt (date unknown; the report itself appears to have been edited multiple times).**
> *On [DATE], at the [LOCATION REDACTED], personnel encountered [DESCRIPTION REDACTED]. The encounter was described in the initial report as [REDACTED, but appears, on close inspection, to have originally read "uneventful"]. The encounter was described in the revised report, filed approximately three weeks later, as [REDACTED, but appears to have originally read "catastrophic"]. The encounter was described in the third revision as [REDACTED]. Subsequent investigation determined that no copies of the original report exist. Subsequent investigation determined that no copies of any report exist. Subsequent investigation determined that the encounter never occurred. This file is being maintained for record-keeping purposes.*

**Mechanical behavior (Phase A).** The Editor is a rare, mid-to-late-campaign mutant. It is humanoid in silhouette, approximately the height of an average adult, dressed in what appear to be the partial remains of a 1970s scholar's tweed jacket. Its face is obscured by a sheet of paper. It does not attack the player. When the Editor enters line of sight, the player's HUD begins to glitch — specifically, the contents of the player's inventory, displayed in the bottom-left corner of the screen, begin to *change*. Items the player picked up earlier in the run are progressively *redacted*, then *deleted*, then *replaced* with different items. By the time the run ends and the Phase A inventory is committed to Phase B, the player's actual bunker inventory will differ from what they remember picking up, in ways proportional to how long the Editor was on screen. The Editor itself is harmless and cannot be killed by conventional means. It can be *distracted*, briefly, by certain documents being thrown at it, which it will stop to read.

**Mechanical behavior (Phase B).** Editor encounters in expedition logs are rare and unsettling. A crew member who encounters an Editor on expedition returns with an inventory that differs from what they took, and a memory that differs from what occurred. The player will, in some cases, find that the crew member's *traits* have been edited — a *Paranoid* crew member returns *Steady*, or vice versa. The Editor is the only known mechanism in the game by which afflictions can be removed *without* a curative event, and is therefore both a hazard and, for desperate players, an opportunity.

**Loot.** The Editor's "face" — the sheet of paper — sometimes falls. Recovered Editor sheets are artifact-class items called **Final Drafts**. A Final Draft, used in the bunker, allows the player to permanently rewrite one stat of one crew member. Use is destructive: the Final Draft is consumed.

**Counter-tactics.** Cover the screen. Look away. If the player cannot see the Editor, the inventory does not edit. Specific maps in Phase A include rooms with broken mirrors and obscured doorways that can be used to navigate without direct line of sight. In Phase B, sending crew members with high literacy stats (Ex-Society Clerks, Ecologists) reduces the magnitude of the Editor's effects, on the grounds that they can recognize the edits as edits.

**Expedition log prose example.**
> "Vera returned this morning. Her pack contains four tins of fish, one med kit, and a Cordon dispatch I did not authorize her to carry. Her pack, when I assigned it to her, contained six tins of fish, one med kit, and a Cordon dispatch I did not authorize her to carry. She does not remember the fish I am missing. She does not remember the fish she has. She does remember the dispatch, which is in her own handwriting, and which she insists she has been carrying since day one."

---

## 6. IMPLEMENTATION HOOKS — UNITY 6 C# AND JSON

This section is the bridge from lore to code. Everything in §§2–5 must be representable in the data layer described here. The architecture follows strict separation of concerns: data lives in ScriptableObjects and JSON, runtime systems consume that data via interfaces, and the UI layer subscribes to runtime systems via events. Nothing in the lore is hardcoded in script.

### 6.1 ScriptableObject Schemas

All schemas live in `Assets/Data/Scripts/Definitions/` and are created via `[CreateAssetMenu]`. All schemas inherit from a base `GameDataObject` for unified handling.

```csharp
// Assets/Data/Scripts/Definitions/GameDataObject.cs
using UnityEngine;

namespace OblastZero.Data
{
    public abstract class GameDataObject : ScriptableObject
    {
        [Header("Core Identity")]
        [Tooltip("Stable string identifier. Used for save-game references, JSON cross-refs, and Steam stats. Never localize.")]
        public string id;

        [Tooltip("Display name shown to the player. Localize this.")]
        public string displayName;

        [TextArea(3, 6)]
        [Tooltip("Internal designer notes. Not shown to player. Use freely for lore context.")]
        public string designerNotes;
    }
}
```

```csharp
// Assets/Data/Scripts/Definitions/FactionData.cs
using System.Collections.Generic;
using UnityEngine;

namespace OblastZero.Data
{
    public enum FactionId
    {
        None = 0,
        ScaleSociety = 10,
        Cordon = 20,
        Kafedra = 30,
        Loners = 40,
        Bandits = 50
    }

    [System.Serializable]
    public struct FactionRelation
    {
        public FactionId other;
        [Range(-100, 100)] public int defaultStanding;
    }

    [System.Serializable]
    public struct ReputationThreshold
    {
        public string thresholdName; // "Hunted", "Hostile", "Neutral", "Allied", "Endgame"
        public int minReputation;
        public int maxReputation;
    }

    [CreateAssetMenu(menuName = "OblastZero/Faction", fileName = "Faction_")]
    public class FactionData : GameDataObject
    {
        [Header("Identity")]
        public FactionId factionId;
        public Color factionColor;
        public Sprite factionEmblem;

        [Header("Ideology Tags")]
        [Tooltip("Free-form tags used by the event engine to match faction-flavored events.")]
        public List<string> ideologyTags; // e.g. "bureaucratic", "demographic", "actuarial"

        [Header("Inter-Faction Relations")]
        public List<FactionRelation> baseRelations;

        [Header("Reputation Bands")]
        public List<ReputationThreshold> thresholds;

        [Header("Voice / Flavor")]
        public VoiceLineGroup radioChatter;
        public VoiceLineGroup combatBarks;

        [Header("Signature Equipment")]
        public List<ItemData> signatureEquipment;

        [Header("Endgame")]
        public string endgameBranchId; // referenced by the event engine to gate the faction-specific ending
    }
}
```

```csharp
// Assets/Data/Scripts/Definitions/AnomalyData.cs
using UnityEngine;
using System.Collections.Generic;

namespace OblastZero.Data
{
    public enum AnomalyHazardType
    {
        Cognitive,     // Interview, Editor-adjacent — mind-state hazards
        Temporal,      // Backlog, time-pooling
        Duplicative,   // Carbon Copy — produces erroneous duplicates
        Gravitational, // STALKER-style spatial hazards
        Thermal,
        Electrical,
        Chemical,
        Psionic
    }

    [System.Serializable]
    public struct DamageProfile
    {
        public float healthPerSecond;
        public float radiationPerSecond;
        public float sanityPerExposure;
        public bool causesPermanentTrait;
        public string permanentTraitId;
    }

    [System.Serializable]
    public struct WeightedItem
    {
        public ItemData item;
        [Range(0f, 1f)] public float dropChance;
        public int minQty;
        public int maxQty;
    }

    [CreateAssetMenu(menuName = "OblastZero/Anomaly", fileName = "Anomaly_")]
    public class AnomalyData : GameDataObject
    {
        [Header("Classification")]
        public string classificationCode; // "ANM-Δ-07/CC"
        public string fieldName;           // "The carbon", "the desk drawer"

        [Header("Hazard Profile")]
        public AnomalyHazardType primaryHazard;
        public DamageProfile damageProfile;
        public float effectiveRadiusMeters;
        public bool visibleToNakedEye;
        public bool detectableByGeiger;

        [Header("Drops / Artifacts")]
        public List<WeightedItem> artifactDropTable;

        [Header("Phase B (Expedition Log)")]
        [TextArea(2, 5)] public string expeditionEncounterTextKey; // localization key
        public ExpeditionEventData expeditionEvent;                // optional dedicated event trigger
    }
}
```

```csharp
// Assets/Data/Scripts/Definitions/MutantData.cs
using UnityEngine;
using System.Collections.Generic;

namespace OblastZero.Data
{
    public enum MutantBehaviorType
    {
        AmbushPredator,
        SlowStalker,    // Drowned Census-Taker
        PsychicHazard,  // Editor
        SwarmHarasser,
        BurrowingAttacker,
        SpecialEncounter
    }

    [System.Serializable]
    public struct HealthProfile
    {
        public int maxHealth;
        public int armorPiercingThreshold;
        public bool immuneToConventionalFirearms;
        public bool requiresArtifactToKill;
    }

    [CreateAssetMenu(menuName = "OblastZero/Mutant", fileName = "Mutant_")]
    public class MutantData : GameDataObject
    {
        [Header("Classification")]
        public string classificationCode; // "MTN-Β-04/DC"
        public string fieldName;

        [Header("Behavior")]
        public MutantBehaviorType behavior;
        public HealthProfile health;
        public float moveSpeed;
        public float sightRangeMeters;
        public float aggroRangeMeters;

        [Header("Hazards")]
        public DamageProfile contactDamage;
        public int fearFactor; // sanity drain on visual encounter, 0–100

        [Header("Loot")]
        public List<WeightedItem> lootTable;

        [Header("Phase B")]
        [TextArea(2, 5)] public string expeditionEncounterTextKey;
        public ExpeditionEventData expeditionEvent;
    }
}
```

```csharp
// Assets/Data/Scripts/Definitions/CrewMemberData.cs
using UnityEngine;
using System.Collections.Generic;

namespace OblastZero.Data
{
    public enum CrewBackground
    {
        LonerScavenger,
        ExCordonSoldier,
        ExSocietyClerk,
        FieldMedic,
        Mechanic,
        KafedraDefector,
        EcologistSurvivor
    }

    [System.Serializable]
    public struct CrewBaseStats
    {
        [Range(0, 100)] public int maxHealth;
        [Range(0, 100)] public int maxSanity;
        public float carryCapacityKg;
        [Range(0f, 2f)] public float sanityRecoveryMultiplier;
        [Range(0f, 2f)] public float radiationResistanceMultiplier;
        [Range(0f, 2f)] public float combatResolutionMultiplier;
    }

    [CreateAssetMenu(menuName = "OblastZero/Crew Member", fileName = "Crew_")]
    public class CrewMemberData : GameDataObject
    {
        [Header("Identity")]
        public string firstName;
        public string lastName;
        public string patronymic;
        public CrewBackground background;
        public Sprite portrait;

        [Header("Base Stats")]
        public CrewBaseStats baseStats;

        [Header("Starting Traits")]
        public List<TraitData> startingTraits;

        [Header("Voice / Personality")]
        public VoiceLineGroup voiceLineGroup;

        [Header("Backstory")]
        [TextArea(3, 10)] public string backstoryText;
    }
}
```

```csharp
// Assets/Data/Scripts/Definitions/ItemData.cs
using UnityEngine;
using System.Collections.Generic;

namespace OblastZero.Data
{
    public enum ItemCategory
    {
        Food,
        Water,
        Medical,
        Weapon,
        Ammunition,
        Tool,
        Document,
        Artifact,
        Crafting,
        Special
    }

    public enum UtilityTag
    {
        Eat,
        Drink,
        Heal,
        Repair,
        Fight,
        Trade,
        Read,
        Decontaminate,
        Defend,
        Ritual
    }

    [CreateAssetMenu(menuName = "OblastZero/Item", fileName = "Item_")]
    public class ItemData : GameDataObject
    {
        [Header("Basic")]
        public ItemCategory category;
        public Sprite icon;
        public GameObject worldPrefab; // for Phase A pickup

        [Header("Physical")]
        public float weightKg;
        [Range(0, 100)] public int durability;
        public float decayPerDay;

        [Header("Multi-Utility (60 Seconds! DNA)")]
        [Tooltip("Tags describing every use this item supports. An axe might have [Repair, Fight, Ritual].")]
        public List<UtilityTag> utilityTags;

        [Header("Hazard")]
        public bool radiationContaminated;
        public float radiationContaminationLevel;

        [Header("Trade Values")]
        public int baseTradeValueScale;
        public int baseTradeValueCordon;
        public int baseTradeValueKafedra;
    }
}
```

```csharp
// Assets/Data/Scripts/Definitions/ExpeditionEventData.cs
using UnityEngine;
using System.Collections.Generic;

namespace OblastZero.Data
{
    [System.Serializable]
    public struct EventPrerequisite
    {
        public int minDay;
        public int maxDay;
        public FactionId factionContext;
        [Range(-100, 100)] public int minFactionRep;
        [Range(-100, 100)] public int maxFactionRep;
        public List<string> requiredCrewTraitIds;
        public List<ItemData> requiredItemsAny;
        public List<string> regionTagsAny;
    }

    [System.Serializable]
    public struct OutcomeDelta
    {
        public int sanityDelta;
        public int fatigueDelta;
        public int radiationDelta;
        public int healthDelta;
        public List<WeightedItem> lootGained;
        public List<ItemData> itemsLost;
        public FactionId reputationFaction;
        public int reputationDelta;
        public string followUpEventId;
        [Range(0f, 1f)] public float crewDeathChance;
    }

    [System.Serializable]
    public struct EventChoice
    {
        public string choiceLabelKey; // localization key
        public List<string> requiredTraitsAny;
        public List<string> blockedByTraits;
        public OutcomeDelta successOutcome;
        public OutcomeDelta failureOutcome;
        [Range(0f, 1f)] public float successChance;
        public string successChanceFormula; // optional: formula evaluated against crew stats at runtime
    }

    [CreateAssetMenu(menuName = "OblastZero/Expedition Event", fileName = "Event_")]
    public class ExpeditionEventData : GameDataObject
    {
        [Header("Narrative")]
        public string titleKey;
        [TextArea(4, 10)] public string narrativeTextKey;

        [Header("Trigger Conditions")]
        public EventPrerequisite prerequisites;
        [Range(0f, 1f)] public float baseWeight;

        [Header("Branches")]
        public List<EventChoice> choices;

        [Header("Source")]
        public string sourceJsonPath; // if loaded from JSON at runtime
    }
}
```

A few supporting types referenced above (`TraitData`, `VoiceLineGroup`) follow the same pattern: lightweight `ScriptableObject` definitions in `Assets/Data/Scripts/Definitions/`. `TraitData` carries an id, display name, description, stat modifiers, and flags for whether the trait is a virtue or affliction. `VoiceLineGroup` carries an id and a list of `AudioClip` references plus optional subtitle keys.

### 6.2 JSON Expedition Event Payloads

The 2D phase's narrative engine loads `ExpeditionEventData` from both ScriptableObjects (authored in-editor) and JSON files (authored in bulk by writers and AI tools) at `Assets/Data/Resources/Events/*.json`. Each JSON file defines a single event using the schema below.

**Event #1 — Scale Society Census Enforcer.**

```json
{
  "id": "evt_scale_census_001",
  "titleKey": "evt.scale.census_001.title",
  "narrativeText": "Inna returns from the Cordon road at dusk. A grey GAZ sedan blocked the access path. Two clerks. One was holding a folder with our bunker's nominal coordinates printed inside, dated last week. The senior clerk — registration number 1148 — informed her that 'an irregularity in regional headcount' had been identified and that a 'voluntary supplementary registration' was requested for 'all personnel currently residing in the area.' She returned without incident. They followed her for three kilometers. They did not approach. They wrote.",
  "prerequisites": {
    "minDay": 6,
    "maxDay": 999,
    "factionContext": "ScaleSociety",
    "minFactionRep": -20,
    "maxFactionRep": 40,
    "requiredCrewTraitIds": [],
    "requiredItemsAny": [],
    "regionTagsAny": ["outer_cordon", "census_district"]
  },
  "baseWeight": 0.65,
  "choices": [
    {
      "choiceLabelKey": "evt.scale.census_001.choice.comply",
      "successChance": 1.0,
      "successOutcome": {
        "sanityDelta": -5,
        "reputationFaction": "ScaleSociety",
        "reputationDelta": 10,
        "lootGained": [
          { "itemId": "item_food_tushonka", "minQty": 2, "maxQty": 3, "dropChance": 1.0 }
        ],
        "followUpEventId": "evt_scale_census_002_follow"
      }
    },
    {
      "choiceLabelKey": "evt.scale.census_001.choice.refuse_polite",
      "successChance": 0.7,
      "successChanceFormula": "0.3 + 0.4 * (crew.charisma / 100)",
      "successOutcome": {
        "sanityDelta": -10,
        "reputationFaction": "ScaleSociety",
        "reputationDelta": -10
      },
      "failureOutcome": {
        "sanityDelta": -20,
        "reputationFaction": "ScaleSociety",
        "reputationDelta": -30,
        "followUpEventId": "evt_scale_enforcement_001"
      }
    },
    {
      "choiceLabelKey": "evt.scale.census_001.choice.ambush",
      "requiredTraitsAny": ["trait_ex_cordon", "trait_steady"],
      "successChance": 0.55,
      "successChanceFormula": "0.2 + 0.5 * (crew.combat / 100) - 0.2 * crew.fatigue_norm",
      "successOutcome": {
        "sanityDelta": -15,
        "reputationFaction": "ScaleSociety",
        "reputationDelta": -40,
        "reputationFactionSecondary": "Cordon",
        "reputationSecondaryDelta": 10,
        "lootGained": [
          { "itemId": "item_pistol_pm", "minQty": 1, "maxQty": 1, "dropChance": 1.0 },
          { "itemId": "item_doc_scale_clipboard", "minQty": 1, "maxQty": 1, "dropChance": 1.0 }
        ]
      },
      "failureOutcome": {
        "sanityDelta": -25,
        "healthDelta": -40,
        "crewDeathChance": 0.35,
        "reputationFaction": "ScaleSociety",
        "reputationDelta": -50
      }
    }
  ]
}
```

**Event #2 — Remnant Cordon Patrol Crossfire.**

```json
{
  "id": "evt_cordon_crossfire_001",
  "titleKey": "evt.cordon.crossfire_001.title",
  "narrativeText": "Pavel crested the rise above the Grain Belt rail line and dropped flat. Below him, a Cordon patrol — four soldiers, AKM rifles, the pale-grey uniforms — was engaging a Scale Society 'labor recovery' detail across the tracks. The Cordon had the elevation. The Society had a vehicle and one man with a long rifle. Pavel had a clear line of fire on both. He has approximately ninety seconds before someone notices him.",
  "prerequisites": {
    "minDay": 4,
    "maxDay": 999,
    "factionContext": "None",
    "minFactionRep": -100,
    "maxFactionRep": 100,
    "requiredCrewTraitIds": [],
    "requiredItemsAny": ["item_rifle_any"],
    "regionTagsAny": ["grain_belt"]
  },
  "baseWeight": 0.45,
  "choices": [
    {
      "choiceLabelKey": "evt.cordon.crossfire_001.choice.support_cordon",
      "successChance": 0.7,
      "successChanceFormula": "0.4 + 0.5 * (crew.combat / 100)",
      "successOutcome": {
        "sanityDelta": -10,
        "reputationFaction": "Cordon",
        "reputationDelta": 15,
        "reputationFactionSecondary": "ScaleSociety",
        "reputationSecondaryDelta": -20,
        "lootGained": [
          { "itemId": "item_doc_cordon_dispatch", "minQty": 1, "maxQty": 1, "dropChance": 0.5 }
        ]
      },
      "failureOutcome": {
        "sanityDelta": -15,
        "healthDelta": -30,
        "reputationFaction": "Cordon",
        "reputationDelta": 5,
        "reputationFactionSecondary": "ScaleSociety",
        "reputationSecondaryDelta": -25
      }
    },
    {
      "choiceLabelKey": "evt.cordon.crossfire_001.choice.support_scale",
      "successChance": 0.6,
      "successOutcome": {
        "sanityDelta": -15,
        "reputationFaction": "ScaleSociety",
        "reputationDelta": 20,
        "reputationFactionSecondary": "Cordon",
        "reputationSecondaryDelta": -30,
        "lootGained": [
          { "itemId": "item_food_ration_pack", "minQty": 2, "maxQty": 4, "dropChance": 1.0 }
        ]
      },
      "failureOutcome": {
        "sanityDelta": -20,
        "healthDelta": -40,
        "crewDeathChance": 0.2
      }
    },
    {
      "choiceLabelKey": "evt.cordon.crossfire_001.choice.scavenge_afterward",
      "successChance": 0.75,
      "successOutcome": {
        "sanityDelta": -8,
        "fatigueDelta": 15,
        "lootGained": [
          { "itemId": "item_ammo_762", "minQty": 8, "maxQty": 20, "dropChance": 1.0 },
          { "itemId": "item_medkit_field", "minQty": 1, "maxQty": 2, "dropChance": 0.7 },
          { "itemId": "item_doc_cordon_dispatch", "minQty": 1, "maxQty": 1, "dropChance": 0.3 }
        ]
      },
      "failureOutcome": {
        "sanityDelta": -12,
        "healthDelta": -20,
        "reputationFaction": "Cordon",
        "reputationDelta": -10
      }
    },
    {
      "choiceLabelKey": "evt.cordon.crossfire_001.choice.withdraw",
      "successChance": 1.0,
      "successOutcome": {
        "sanityDelta": -3,
        "fatigueDelta": 8
      }
    }
  ]
}
```

**Event #3 — Kafedra Recruitment Offer.**

```json
{
  "id": "evt_kafedra_offer_001",
  "titleKey": "evt.kafedra.offer_001.title",
  "narrativeText": "Marina was approached at the Reservoir overflow by a woman she described as 'wearing a wooden mask carved with concentric circles, and a surgical apron that had been mended at the shoulder.' The woman did not introduce herself. She offered Marina tea, which Marina accepted, which Marina now regrets. She said: 'We have read about you. You are listening more than the others. There is room for you at the Pansionat. Come when you are tired.' She gave Marina a folded paper and walked into the water. The water came up to her chest. She kept walking. Marina stopped watching at that point.\\n\\nThe paper, opened, contains coordinates and one phrase: 'Bring no one you are not willing to be without.'",
  "prerequisites": {
    "minDay": 10,
    "maxDay": 999,
    "factionContext": "Kafedra",
    "minFactionRep": 0,
    "maxFactionRep": 100,
    "requiredCrewTraitIds": [],
    "requiredItemsAny": [],
    "regionTagsAny": ["reservoir", "inner_ring", "bureau_quarter"]
  },
  "baseWeight": 0.3,
  "choices": [
    {
      "choiceLabelKey": "evt.kafedra.offer_001.choice.accept_visit",
      "successChance": 1.0,
      "successOutcome": {
        "sanityDelta": -10,
        "reputationFaction": "Kafedra",
        "reputationDelta": 25,
        "reputationFactionSecondary": "ScaleSociety",
        "reputationSecondaryDelta": -10,
        "followUpEventId": "evt_kafedra_pansionat_visit"
      }
    },
    {
      "choiceLabelKey": "evt.kafedra.offer_001.choice.burn_paper",
      "successChance": 1.0,
      "successOutcome": {
        "sanityDelta": 5,
        "reputationFaction": "Kafedra",
        "reputationDelta": -15
      }
    },
    {
      "choiceLabelKey": "evt.kafedra.offer_001.choice.report_to_scale",
      "requiredTraitsAny": [],
      "blockedByTraits": ["trait_kafedra_marked"],
      "successChance": 1.0,
      "successOutcome": {
        "sanityDelta": -5,
        "reputationFaction": "ScaleSociety",
        "reputationDelta": 15,
        "reputationFactionSecondary": "Kafedra",
        "reputationSecondaryDelta": -35,
        "lootGained": [
          { "itemId": "item_food_ration_pack", "minQty": 3, "maxQty": 3, "dropChance": 1.0 }
        ],
        "followUpEventId": "evt_kafedra_reprisal_001"
      }
    }
  ]
}
```

**Event #4 — Anomaly Hazard: The Backlog.**

```json
{
  "id": "evt_anomaly_backlog_001",
  "titleKey": "evt.anomaly.backlog_001.title",
  "narrativeText": "Olga's report, transcribed from her own field journal upon return:\\n\\n'Day twelve, midday by my reckoning. The southern wing of the Bureau is colder than the rest of the building, and there is a region in the third corridor where dust does not fall. I tested it with a coin: thrown in, it slowed and stopped, six inches above the floor, suspended. I marked the corridor with chalk and went around.\\n\\n'I am writing this on day twelve, midday by my reckoning. I have been writing for some time. There is a window at the end of the corridor. The light through the window has not changed. I do not know how long I have been here. I am going to leave by the way I came. If you are reading this, the dates will tell us.'\\n\\nOlga returned to the bunker on day twenty-one. She believes today is day thirteen. She has not been told otherwise.",
  "prerequisites": {
    "minDay": 8,
    "maxDay": 999,
    "factionContext": "None",
    "minFactionRep": -100,
    "maxFactionRep": 100,
    "requiredCrewTraitIds": [],
    "requiredItemsAny": [],
    "regionTagsAny": ["bureau_quarter", "inner_ring"]
  },
  "baseWeight": 0.35,
  "choices": [
    {
      "choiceLabelKey": "evt.anomaly.backlog_001.choice.tell_truth",
      "successChance": 1.0,
      "successOutcome": {
        "sanityDelta": -25,
        "fatigueDelta": -10,
        "followUpEventId": "evt_anomaly_backlog_002_aftermath",
        "appliesTraitId": "trait_witnessed"
      }
    },
    {
      "choiceLabelKey": "evt.anomaly.backlog_001.choice.keep_quiet",
      "successChance": 1.0,
      "successOutcome": {
        "sanityDelta": -5,
        "fatigueDelta": -10,
        "appliesTraitId": "trait_hollow"
      }
    },
    {
      "choiceLabelKey": "evt.anomaly.backlog_001.choice.send_kafedra",
      "requiredTraitsAny": [],
      "blockedByTraits": [],
      "successChance": 1.0,
      "successOutcome": {
        "sanityDelta": -15,
        "reputationFaction": "Kafedra",
        "reputationDelta": 15,
        "lootGained": [
          { "itemId": "item_medkit_kafedra", "minQty": 1, "maxQty": 1, "dropChance": 1.0 }
        ]
      }
    }
  ]
}
```

**Event #5 — Mutant Ambush: The Drowned Census-Taker.**

```json
{
  "id": "evt_mutant_drowned_001",
  "titleKey": "evt.mutant.drowned_001.title",
  "narrativeText": "Pavel reports a sighting near the Reservoir overflow, at the partially submerged stretch where the old village rooftops break the surface. He saw it from approximately seventy meters. It was wearing a Scale Society long coat, mostly intact, dark with water. It was carrying a clipboard. It saw him at the same moment he saw it. It did not pursue. It began walking, at a steady pace, in his direction. He could not retreat into the village without losing his footing on the submerged pavement. He could not advance without crossing standing water. The thing was approximately forty meters away when he wrote this. It was speaking. He says — and he has asked me to write this down exactly — that what it was saying, quietly and conversationally, as if confirming a spelling, was the name of his mother.",
  "prerequisites": {
    "minDay": 3,
    "maxDay": 999,
    "factionContext": "None",
    "minFactionRep": -100,
    "maxFactionRep": 100,
    "requiredCrewTraitIds": [],
    "requiredItemsAny": [],
    "regionTagsAny": ["reservoir", "census_district"]
  },
  "baseWeight": 0.5,
  "choices": [
    {
      "choiceLabelKey": "evt.mutant.drowned_001.choice.fight",
      "requiredTraitsAny": [],
      "successChance": 0.55,
      "successChanceFormula": "0.2 + 0.5 * (crew.combat / 100) - 0.15 * crew.fatigue_norm",
      "successOutcome": {
        "sanityDelta": -20,
        "healthDelta": -10,
        "lootGained": [
          { "itemId": "item_doc_drowned_clipboard", "minQty": 1, "maxQty": 1, "dropChance": 1.0 },
          { "itemId": "item_pistol_pm", "minQty": 1, "maxQty": 1, "dropChance": 0.4 }
        ]
      },
      "failureOutcome": {
        "sanityDelta": -35,
        "healthDelta": -50,
        "crewDeathChance": 0.25,
        "appliesTraitId": "trait_compromised"
      }
    },
    {
      "choiceLabelKey": "evt.mutant.drowned_001.choice.run",
      "successChance": 0.75,
      "successChanceFormula": "0.3 + 0.5 * (1.0 - crew.fatigue_norm)",
      "successOutcome": {
        "sanityDelta": -10,
        "fatigueDelta": 20
      },
      "failureOutcome": {
        "sanityDelta": -25,
        "healthDelta": -15,
        "appliesTraitId": "trait_compromised"
      }
    },
    {
      "choiceLabelKey": "evt.mutant.drowned_001.choice.let_it_register",
      "successChance": 1.0,
      "successOutcome": {
        "sanityDelta": -15,
        "appliesTraitId": "trait_registered",
        "lootGained": [
          { "itemId": "item_doc_drowned_clipboard", "minQty": 1, "maxQty": 1, "dropChance": 1.0 }
        ]
      }
    }
  ]
}
```

### 6.3 State Machine and Data Flow (3D → 2D → Meta)

The game runs a single root state machine, `GameStateMachine`, with the following states and transitions. Every state is a `MonoBehaviour` implementing `IGameState`, and the state machine itself is a `MonoBehaviour` singleton living on a `_Bootstrap` scene that is never unloaded.

```csharp
// Assets/_Project/Scripts/Core/IGameState.cs
namespace OblastZero.Core
{
    public interface IGameState
    {
        string StateId { get; }
        void OnEnter(StateContext context);
        void OnExit(StateContext context);
        void OnTick(float deltaTime);
    }
}
```

```csharp
// Assets/_Project/Scripts/Core/StateContext.cs
namespace OblastZero.Core
{
    public class StateContext
    {
        public RunData CurrentRun { get; set; }
        public MetaProgressData MetaProgress { get; set; }
    }
}
```

The states are:

`MainMenuState` — entry. Loads `MetaProgressData` from disk. On "New Run" click, creates a fresh `RunData` and transitions to `RunSetupState`.

`RunSetupState` — character selection, scavenge site selection, difficulty modifiers. Pushes the seed into `RunData`. Transitions to `ScavengePhase3DState`.

`ScavengePhase3DState` — loads the 3D scavenge scene additively, instantiates the player, kicks off the 60-second `EmissionTimer`, and listens for the player's pickup events. Items picked up are written into `RunData.ScavengedInventory` as `ItemInstance` records (item id + condition + contamination level). Crew members rescued are written into `RunData.RescuedCrew` as `CrewInstance` records (crew member id + current health + current sanity + current radiation). On timer expiry or "Reach Bunker" trigger, transitions to `TransitionCutsceneState`.

`TransitionCutsceneState` — a brief sealed-bunker-door cinematic. While the cinematic plays, the state's `OnEnter` performs the actual data handoff: `RunData.ScavengedInventory` is committed to `RunData.BunkerInventory`, `RunData.RescuedCrew` becomes `RunData.ActiveCrew`, and the 3D scene is unloaded. Transitions to `SurvivalPhase2DState` when the cinematic ends.

`SurvivalPhase2DState` — loads the 2D bunker scene additively. This is the long state — the player may remain here for the bulk of the run. It owns the day-advance loop described in §4, the `EventEngine` (which loads `ExpeditionEventData` from ScriptableObjects and JSON), the `FactionReputationManager`, and the `CrewManager`. Each in-game day advance is a single `OnTick` substep. Transitions to one of:

- `RunFailedState` (all crew dead, bunker breach, or specific narrative failure events)
- `RunVictoryState_Stabilization` (Scale Society endgame triggered)
- `RunVictoryState_Relief` (Cordon endgame triggered)
- `RunVictoryState_Adaptation` (Kafedra endgame triggered)
- `RunVictoryState_Independent` (rare neutral-ending branch)

`RunVictoryState_*` and `RunFailedState` — present the run summary, calculate meta-progression rewards (new unlockable scavenge sites, new starting crew options, new starting equipment kits), write `MetaProgressData` to disk, and transition back to `MainMenuState`.

A single canonical `RunData` object is the source of truth for the entire run. It is serialized to JSON on every day advance (autosave) and on every state transition. On a Steam Cloud build, the JSON is mirrored to the cloud save location.

```csharp
// Assets/_Project/Scripts/Core/RunData.cs
using System;
using System.Collections.Generic;

namespace OblastZero.Core
{
    [Serializable]
    public class RunData
    {
        public string runId;
        public DateTime runStartedUtc;
        public int currentDay;
        public string currentScavengeSiteId;

        // Phase A handoff
        public List<ItemInstance> ScavengedInventory = new();
        public List<CrewInstance> RescuedCrew = new();

        // Phase B persistent state
        public List<ItemInstance> BunkerInventory = new();
        public List<CrewInstance> ActiveCrew = new();
        public List<ActiveExpedition> ExpeditionsInFlight = new();
        public List<string> CompletedEventIds = new();
        public List<string> QueuedEventIds = new();

        // Faction reputation
        public int repScaleSociety;
        public int repCordon;
        public int repKafedra;

        // Environmental state
        public int bunkerRadiationPool;
        public int bunkerMorale;
        public bool bunkerSealed;

        // RNG
        public int rngSeed;
        public int rngStreamCounter;
    }

    [Serializable]
    public class ItemInstance
    {
        public string itemDataId;
        public int currentDurability;
        public float currentContamination;
        public int quantity;
    }

    [Serializable]
    public class CrewInstance
    {
        public string crewDataId;
        public string instanceId; // unique per crew member, persists across runs if recruited again
        public int currentHealth;
        public int currentSanity;
        public int currentFatigue;
        public int currentRadiation;
        public List<string> traitIds = new();
        public bool isAlive;
        public string locationTag; // "bunker", "expedition:reservoir", "missing", "dead_recoverable"
    }

    [Serializable]
    public class ActiveExpedition
    {
        public string expeditionId;
        public string crewInstanceId;
        public string regionTag;
        public int dayStarted;
        public int duration;
        public List<string> loadoutItemInstanceIds;
        public List<string> resolvedEventIds = new();
    }
}
```

`MetaProgressData` lives separately from `RunData` — it tracks the player's overall progress across runs:

```csharp
// Assets/_Project/Scripts/Core/MetaProgressData.cs
using System;
using System.Collections.Generic;

namespace OblastZero.Core
{
    [Serializable]
    public class MetaProgressData
    {
        public int totalRunsAttempted;
        public int totalRunsSurvived;
        public List<string> unlockedScavengeSites = new();
        public List<string> unlockedStartingKits = new();
        public List<string> unlockedCrewArchetypes = new();
        public List<string> discoveredAnomalyIds = new();
        public List<string> discoveredMutantIds = new();
        public List<string> recoveredDocumentIds = new();
        public List<string> unlockedEndings = new();
        public Dictionary<string, int> steamStats = new();
    }
}
```

The full data flow, end to end:

A new run begins from the `MainMenuState`, which constructs a fresh `RunData`. The player passes through `RunSetupState`, picks a scavenge site, and enters `ScavengePhase3DState`. As they pick up items and crew, those go straight into `RunData.ScavengedInventory` and `RunData.RescuedCrew`. The Emission fires; the player reaches the bunker; `TransitionCutsceneState` migrates that data into `BunkerInventory` and `ActiveCrew`. `SurvivalPhase2DState` now owns the run. It consumes `ExpeditionEventData` from the data layer, presents events through the UI layer, applies outcomes back into `RunData`, and on each day advance both autosaves the run and rolls forward. Whenever the run ends — death, ending, or quit — the run-end states commit accumulated discoveries, achievements, and unlocks into `MetaProgressData`, which then carries forward to all subsequent runs.

No script outside `Assets/_Project/Scripts/Core/` ever writes to `RunData` directly. All mutation is mediated through manager classes (`InventoryManager`, `CrewManager`, `FactionReputationManager`, etc.) that fire events on every change, which the UI layer subscribes to. This is the strict separation of concerns called for in the project's mission directive: logic mutates data, UI reads data and listens for change events, and the data itself is just `RunData` and the ScriptableObjects it points at.

---

## 7. CONTENT GENERATION STYLE GUIDE

Future writers — human or AI — generating expedition events, document fragments, radio chatter, or crew dialogue should adhere to the following voice rules. The tone is the game's primary product. Drift kills atmosphere faster than bugs.

**Vocabulary banks.** Lean on Soviet and post-Soviet bureaucratic register. Words and phrases that should appear regularly include: *registered*, *line item*, *deviation*, *irregularity*, *protocol*, *pending review*, *adjustment*, *supplemental*, *containment*, *cordoned*, *interdiction*, *administrative*, *quarterly*, *quota*, *requisition*, *dispatch*, *standing order*. Russian terms used sparingly and never in italics in-line: *выброс* (the Emission), *тяжесть* (the heaviness), *правка* (an edit), *кордон* (the cordon). Dates should be written in European format (11.iv.1987). Personnel are referred to by surname-initial-patronymic.

**Redaction patterns.** When writing in-fiction document fragments, redact names, dates, locations, and specific technical terms with `[REDACTED]`, but redact *unevenly*. A real redaction is unevenly applied — sometimes the censor missed a reference, sometimes a name appears in one paragraph and is redacted in the next, sometimes the redaction is in the wrong place. This is where the texture lives.

**Decay imagery.** Concrete is *stained*, not *broken*. Paint is *peeling*, not *gone*. Equipment is *operational*, never *new*. Light is *low*, *fluorescent*, *failing*. Air is *thick*, *cold*, *wet*. Avoid: anything described as *post-apocalyptic*, *wasteland*, *ruined*. We are not post-apocalyptic. We are post-administrative. Things did not end. Things just stopped being maintained.

**Forbidden clichés.** Avoid: "twisted metal," "skeletal remains," "an eerie silence," "an unnatural glow," "the hairs on the back of his neck," "screams in the distance," "the smell of death." We are not writing pulp horror. We are writing administrative report fiction in a register that should remind the reader of every dull document they have ever been forced to read in real life, with the horror underneath.

**Sample sentence structures.**

The simple declarative bureaucratic sentence: *"The patrol returned. Casualties: one. Documentation will follow."*

The understated horror sentence: *"The third member emerged from the region on 14.ix.2009. He was not visibly aged. He was carrying a paper bag containing his lunch, which he reported as still warm."*

The redacted memo line: *"Subsequent investigation determined that the encounter never occurred. This file is being maintained for record-keeping purposes."*

The Kafedra voice (the only register in the game with anything like warmth, and *only* in their published material — never their direct dialogue, which is clipped): *"The error of the State was to suppose that the Field was an event. The Field is a register. We have been writing on the wrong page."*

The crew journal voice (the only register that allows the player a moment of human feeling): *"Marina has not slept. I do not know how to tell her which of the two crates to keep. I do not think either is the right one."*

Use these as anchors. Vary cadence and length to avoid monotony, but stay inside the register. The Oblast does not raise its voice. The Oblast files a form.

---

*End of Foundational Document. Subsequent expansions: Bestiary Volume II (additional mutants, additional anomalies), Faction Bible expansions (named NPCs per faction with full bio sheets), the Document Archive (the corpus of in-fiction recoverable papers, currently planned at 80+ unique documents), and the Audio Bible (voice line groups per faction and per crew background).*
