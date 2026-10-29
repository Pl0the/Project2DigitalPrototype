# Clonetastic Four

A local, hot-seat 1v1 arena prototype. Use the Unity version listed in `ProjectSettings/ProjectVersion.txt`, open `Assets/Scenes/ClonetasticArena.unity`, and press Play.

The project uses Git LFS. After cloning, run `git lfs install` and `git lfs pull` before opening Unity.

## Playing on one computer

1. Press Enter to start card setup. The overhead board is public.
2. Press H, ask the other player to look away, then press Space to reveal your hand. Click a card or press its number.
3. An Attack queues one shot and passes the turn. A Block returns to the board for placement; the card is spent only after a valid placement.
4. Take turns playing one card at a time. Press Tab to finish your own setup and save your remaining cards. If one player finishes or empties their hand, the other can keep playing until they finish.
5. Both players take turns moving and aiming in first person. The first player who actually played a card aims last. Confirm each aim with Enter.
6. Watch the overhead resolution. Each Attack adds one five-second firing batch; players with an Attack remaining fire together. Press Enter after resolution for the next round, or a new match after a win or draw.

| Phase | Controls |
| --- | --- |
| Public setup | H: private hand; Tab: finish your setup |
| Private hand | Space: reveal after handoff; 1–6 / click: choose; Esc: return |
| Block placement | Mouse / WASD / arrows: move; Q / E / scroll: rotate 15 degrees |
| Block placement | Left click / Enter / Space: confirm; Esc: cancel; Tab: finish setup |
| First-person aiming | WASD: move; mouse: aim; Enter: confirm; Esc: pause / release mouse |

## Current rules

- Each player starts with 6 HP. A body hit removes 1 HP and reflects the ball; returning shots can damage their own shooter.
- Opening hands have 2 Attack and 3 Block cards. Later rounds draw 1 Attack followed by 2 random Attack/Block cards. The hand limit is 6; overflow discards the newest incoming cards.
- Blocks fit entirely in their owner's half, cannot overlap players or other blocks, and obstruct movement and sight. They reflect balls, lose one durability per hit, and disappear on the third hit. Remaining durability is shown beside each block and carries into later rounds.
- Shots stay on one horizontal plane. Balls pass through other balls. All balls in a firing batch disappear together after 5 seconds before the next batch starts.
- Health and surviving blocks carry between rounds. Knockouts are resolved after the current firing batch; two defeated players produce a draw. Further queued batches stop after a knockout.
- A new match clears hands, blocks, and shot queues, restores both players' positions, and resets health.

The current card set is Attack and Block. Clone cards are the next mechanic to add.
