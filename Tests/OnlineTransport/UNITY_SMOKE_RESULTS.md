# Unity cooperative smoke test

Unity 6000.5.8f1, Windows Development Player, three real processes on TCP loopback.

PASS three peers in lobby
PASS three authoritative player objects
PASS guest movement received
PASS third player movement received
PASS remote player acquires time zero
PASS time zero activated by guest actor
PASS other players remain frozen
caster pos=(8.31, 4.52, 0.00) controls=(0.00, 1.00) enabled=True canAct=True
PASS caster can move during time stop
PASS shop exists
PASS all three individual shops lock after three buys
PASS five rerolls per customer
PASS portal waits for every selection
PASS portal advances all together
PASS each player received own reward
PASS party death opens shared run-over state
PASS restart creates fresh run for all players
SUCCESS

Guest 2:
PASS guest receives party
PASS guest receives render state
SUCCESS

Guest 3:
PASS guest receives party
PASS guest receives render state
SUCCESS


No gameplay exceptions in the final run logs. UI was rendered to offscreen textures for inspection. The entrance room label was subsequently corrected to Entrada and the C# build rechecked.
