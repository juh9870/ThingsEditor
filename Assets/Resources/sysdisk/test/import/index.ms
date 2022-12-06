import "qa"
import "./liba".as("a")
import "./libb".as("b")
import "./libc".as("c")

targ = {}

import "a", "b", "c", from(a).into(targ)
import "a".as("aa"), "b".as("bb"), "c".as("cc"), from(a)

qa.assertEqual a, b
qa.assertEqual a, c
qa.assertEqual a, targ
qa.assertEqual aa, a.a
qa.assertEqual bb, a.b
qa.assertEqual cc, a.c

print "Import paths tests done!"

import "m", from("./libDynamic")
import "m".as("m1"), from("./libDynamic")
import.fresh "m".as("m2"), from("./libDynamic")

m.a = 2
qa.assertEqual m.a, 2
qa.assertEqual m1.a, 2
qa.assertEqual m2.a, 1

print "Import freshness tests done!"

import "./circular/a1".as("c1")

qa.assertEqual c1.consumer, c1.provider

print "Import circular tests done!"