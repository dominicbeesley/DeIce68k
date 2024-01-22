#!/usr/bin/env perl

use strict;

my %ops = ();

while (<>) {
    if (/^[0-9a-f]{6}r\s+\d+\s{2}([0-9a-f]{2})/i) {
        $ops{lc($1)}++;
    }
}

if (scalar keys %ops != 256) {
    print "The following items are missing:\n";

    for my $i (0..255) {
        my $x = sprintf("%02x", $i);
        if (!exists $ops{$x}) {
            print "$x\n";
        }
    }
}
