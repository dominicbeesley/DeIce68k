#!/usr/bin/env perl

#NOTE: if removes directory information from .o/.s filenames and may get
#confused if same symbols in multiple files with same name!

sub Usage($$$) {
	my ($fh, $message, $die) = @_;
	print $fh "
map2sym.pl <map> <.noi out> [<section>=<seg addr>]+

	i.e. map2sym.pl sym.map sym.noi .text=FC00 .data=0100

	";

	if ($die) {
		if ($message) {
			die "$message";
		} else {
			die;
		}
	} else {
		if ($message) {
			print $fh $message;
			print $fh "\n";
		}
	}

}


my $fn_map = shift or Usage(STDERR, "Too few arguments", 1);
open(my $fh_m, "<", $fn_map) or die "Cannot open file \"$fn_map\" : $!";

my $fn_noi = shift or Usage(STDERR, "Too few arguments", 1);
open(my $fh_n, ">", $fn_noi) or die "Cannot open output file \"$fn_noi\" : $!";

my %segs=();

while (my $a = shift) {

	$a =~ /^\s*([\w\.]+)\s*=\s*([0-9a-f]+)\s*$/i or die "Bad .section=ADDR : $a";
	$segs{$1}=hex($2);
}

my $cursec;
my $symbols;

while (<$fh_m>) {


	if (!$symbols) {

		if ($_ =~ /^--\sSymbols/) {
			$symbols = 1;
		}
	} else {

		if ($_ =~ /^----\sSection\s([\.\w]+)/) {
			$cursec = $1;
		} elsif ($_ =~ /^\s*([0-9A-F]+)\s*([0-9A-F]+)\s*([\w\.]+)/) {

			my $sym = $3;
			my $add = $1;

			$sym =~ s/\./_/g;

			if (exists $segs{$cursec}) {
				my $s = $segs{$cursec};
				
				printf $fh_n "DEF %s %04X%04X\n", $sym, $s & 0xFFFF, hex($add) & 0xFFFF;
			}
		}

	}

}
