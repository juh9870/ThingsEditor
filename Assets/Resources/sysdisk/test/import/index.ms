import "qa";
import "./liba".as("a");
import "./libb".as("b");
import "./libc".as("c");

targ = {};

import "a", "b", "c", from(a).into(targ);
import "a".as("aa"), "b".as("bb"), "c".as("cc"), from(a);

qa.assertEqual(a, b);
qa.assertEqual(a, c);
qa.assertEqual(a, targ);
qa.assertEqual(aa, a.a);
qa.assertEqual(bb, a.b);
qa.assertEqual(cc, a.c);

print("Import tests done!")
