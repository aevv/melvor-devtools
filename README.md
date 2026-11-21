# Dev Tools

Developer tools for Melvor Idle 2 mods: drive and inspect the running game from outside
it, for writing mods without watching the window, and for automated checks.

It does nothing unless the game is started with `MODDEV_DIR=<dir>`. Then write commands,
one per line, to `<dir>/cmd`. They run on the next poll (4 a second), the file is
removed, and `<dir>/done` is written with one line per command.

    shot <name>             <dir>/<name>.png of the main window
    tree <name> [depth]     <dir>/<name>.txt: the scene tree with types, text and sizes
    press <node>            press a button (emits its "pressed" signal)
    call <node> <method>    call a method with no arguments on a node
    click <x> <y>           a left click at window coordinates
    key <name>              press and release a key (Godot key name, e.g. Escape)
    size <w> <h>            resize the window
    quit

`<node>` is a path from the root, or a name (the first visible node called that), or
`name:n` for the nth.

A umitoo mod:

    umitoo run .        try it in the test profile
    umitoo publish .    send it to the registry
