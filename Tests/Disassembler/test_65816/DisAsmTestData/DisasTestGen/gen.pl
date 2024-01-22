#!/usr/bin/env perl

use strict;


my %modes = (
	'-'  		=> {all => ['']},
	'N'  		=> {all => ['0', '10', '100']},
	'#'  		=> {all => ['#0', '#10', '#100']},
	'#M'		=> {all => ['#0', '#10', '#100'], m16 => ['#1000', '#10000']},
	'#X'		=> {all => ['#0', '#10', '#100'], x16 => ['#1000', '#10000']},
	'(a)'		=> {all => ['(LAB1)', '($1234)']},
	'(a,x)'		=> {all => ['(LAB1,X)', '($1234,X)']},
	'(d)'		=> {all => ['(ZLAB1)', '(<$12)']},
	'(d),y'		=> {all => ['(ZLAB1),Y', '(<$12),Y']},
	'(d,s),y'	=> {all => ['(1,S),Y', '($12,S),Y']},
	'(d,x)'		=> {all => ['(ZLAB1,X)', '(<$12,X)']},
	'A'			=> {all => ['A']},
	'[d]'		=> {all => ['[ZLAB1]', '[<$12]']},
	'[d],y'		=> {all => ['[ZLAB1],Y', '[<$12],Y']},
	'a'			=> {all => ['LAB1', '$1234']},
	'a,x'		=> {all => ['LAB1,X', '$1234,X']},
	'a,y'		=> {all => ['LAB1,Y', '$1234,Y']},
	'al'		=> {all => ['f:LABF', 'f:$123456']},
	'al,x'		=> {all => ['f:LABF,X', 'f:$123456,X']},
	'd'			=> {all => ['ZLAB1', '<$12']},
	'd,s'		=> {all => ['1,S', '$12,S']},
	'd,x'		=> {all => ['ZLAB1,X', '<$12,X']},
	'd,y'		=> {all => ['ZLAB1,Y', '<$12,Y']},
	'r'	 		=> {all => ['*', '*+10', '*+100', '*-10', '*-100']},
	'r16' 		=> {all => ['*', '*+10', '*+100', '*-10', '*-100', '*+1000', '*+10000', '*-1000', '*-10000']},
	'xyc'		=> {all => ['#12,#13', '#^ZLAB1, #^LABF']}
);


my @opcodes = (

	# col 0
	{mne => 'BRK', modes => ['N']},
	{mne => 'BPL', modes => ['r']},
	{mne => 'JSR', modes => ['a']},
	{mne => 'BMI', modes => ['r']},
	{mne => 'RTI', modes => ['-']},
	{mne => 'BVC', modes => ['r']},
	{mne => 'RTS', modes => ['-']},
	{mne => 'BVS', modes => ['r']},
	{mne => 'BRA', modes => ['r']},
	{mne => 'BCC', modes => ['r']},
	{mne => 'LDY', modes => ['#X']},
	{mne => 'BCS', modes => ['r']},
	{mne => 'CPY', modes => ['#X']},
	{mne => 'BNE', modes => ['r']},
	{mne => 'CPX', modes => ['#X']},
	{mne => 'BEQ', modes => ['r']},

	# col 1 (3, 5, 7, 9, D, F)
	{mne => 'ORA', modes => ['(d,x)', 'd,s', 'd', '[d]', '#M', 'a', 'al', '(d),y', '(d)', '(d,s),y', 'd,x', '[d],y', 'a,y', 'a,x', 'al,x']},
	{mne => 'AND', modes => ['(d,x)', 'd,s', 'd', '[d]', '#M', 'a', 'al', '(d),y', '(d)', '(d,s),y', 'd,x', '[d],y', 'a,y', 'a,x', 'al,x']},
	{mne => 'EOR', modes => ['(d,x)', 'd,s', 'd', '[d]', '#M', 'a', 'al', '(d),y', '(d)', '(d,s),y', 'd,x', '[d],y', 'a,y', 'a,x', 'al,x']},
	{mne => 'ADC', modes => ['(d,x)', 'd,s', 'd', '[d]', '#M', 'a', 'al', '(d),y', '(d)', '(d,s),y', 'd,x', '[d],y', 'a,y', 'a,x', 'al,x']},
	{mne => 'STA', modes => ['(d,x)', 'd,s', 'd', '[d]',      'a', 'al', '(d),y', '(d)', '(d,s),y', 'd,x', '[d],y', 'a,y', 'a,x', 'al,x']},
	{mne => 'LDA', modes => ['(d,x)', 'd,s', 'd', '[d]', '#M', 'a', 'al', '(d),y', '(d)', '(d,s),y', 'd,x', '[d],y', 'a,y', 'a,x', 'al,x']},
	{mne => 'CMP', modes => ['(d,x)', 'd,s', 'd', '[d]', '#M', 'a', 'al', '(d),y', '(d)', '(d,s),y', 'd,x', '[d],y', 'a,y', 'a,x', 'al,x']},
	{mne => 'SBC', modes => ['(d,x)', 'd,s', 'd', '[d]', '#M', 'a', 'al', '(d),y', '(d)', '(d,s),y', 'd,x', '[d],y', 'a,y', 'a,x', 'al,x']},

	# col 2 even rows
	{mne => 'COP', modes => ['N']},
	{mne => 'JSL', modes => ['al']},
	{mne => 'WDM', modes => ['N']},
	{mne => 'PER', modes => ['r16']},
	{mne => 'BRL', modes => ['r16']},
	{mne => 'LDX', modes => ['#X']},
	{mne => 'REP', modes => ['#']},
	{mne => 'SEP', modes => ['#']},
	

	# col 4
	{mne => 'TSB', modes => ['d', 'a']},
	{mne => 'TRB', modes => ['d', 'a']},
	{mne => 'BIT', modes => ['d', 'a', 'd,x', 'a,x']},
	{mne => 'MVP', modes => ['xyc']},
	{mne => 'MVN', modes => ['xyc']},
	{mne => 'STZ', modes => ['d', 'd,x']},
	{mne => 'STY', modes => ['d', 'a', 'd,x']},
	{mne => 'LDY', modes => ['d', 'a', 'd,x', 'a,x']},
	{mne => 'CPY', modes => ['d', 'a']},
	{mne => 'CPX', modes => ['d', 'a']},

	

	# col 6, E all, (A evens)
	{mne => 'ASL', modes => ['d', 'a', 'd,x', 'a,x']},
	{mne => 'ROL', modes => ['d', 'a', 'd,x', 'a,x']},
	{mne => 'LSR', modes => ['d', 'a', 'd,x', 'a,x']},
	{mne => 'ROR', modes => ['d', 'a', 'd,x', 'a,x']},
	{mne => 'STX', modes => ['d', 'a', 'd,y']},
	{mne => 'LDX', modes => ['d', 'a', 'd,y', 'a,y']},
	{mne => 'DEC', modes => ['d', 'a', 'd,x', 'a,x']},
	{mne => 'INC', modes => ['d', 'a', 'd,x', 'a,x']},

	# col 8
	{mne => 'PHP', modes => ['-']},
	{mne => 'CLC', modes => ['-']},
	{mne => 'PLP', modes => ['-']},
	{mne => 'SEC', modes => ['-']},
	{mne => 'PHA', modes => ['-']},
	{mne => 'CLI', modes => ['-']},
	{mne => 'PLA', modes => ['-']},
	{mne => 'SEI', modes => ['-']},
	{mne => 'DEY', modes => ['-']},
	{mne => 'TYA', modes => ['-']},
	{mne => 'TAY', modes => ['-']},
	{mne => 'CLV', modes => ['-']},
	{mne => 'INY', modes => ['-']},
	{mne => 'CLD', modes => ['-']},
	{mne => 'INX', modes => ['-']},
	{mne => 'SED', modes => ['-']},

	# col A
	{mne => 'ASL', modes => ['A']},
	{mne => 'INC', modes => ['A']},
	{mne => 'ROL', modes => ['A']},
	{mne => 'DEC', modes => ['A']},
	{mne => 'LSR', modes => ['A']},
	{mne => 'PHY', modes => ['-']},
	{mne => 'ROR', modes => ['A']},
	{mne => 'PLY', modes => ['-']},
	{mne => 'TXA', modes => ['-']},
	{mne => 'TXS', modes => ['-']},
	{mne => 'TAX', modes => ['-']},
	{mne => 'TSX', modes => ['-']},
	{mne => 'DEX', modes => ['-']},
	{mne => 'PHX', modes => ['-']},
	{mne => 'NOP', modes => ['-']},
	{mne => 'PLX', modes => ['-']},

	# col B
	{mne => 'PHD', modes => ['-']},
	{mne => 'TCS', modes => ['-']},
	{mne => 'PLD', modes => ['-']},
	{mne => 'TSC', modes => ['-']},
	{mne => 'PHK', modes => ['-']},
	{mne => 'TCD', modes => ['-']},
	{mne => 'RTL', modes => ['-']},
	{mne => 'TDC', modes => ['-']},
	{mne => 'PHB', modes => ['-']},
	{mne => 'TXY', modes => ['-']},
	{mne => 'PLB', modes => ['-']},
	{mne => 'TYX', modes => ['-']},
	{mne => 'WAI', modes => ['-']},
	{mne => 'STP', modes => ['-']},
	{mne => 'XBA', modes => ['-']},
	{mne => 'XCE', modes => ['-']},

	# col C (row & 4 = 4)
	{mne => 'JMP', modes => ['a', 'al', '(a)', '(a,x)']},

	# instead of "STA #" @ 89
	{mne => 'BIT', modes => ['#M']},
	
	# instead of STX a,X @ 9E
	{mne => 'STZ', modes => ['a,x']},

	# instead of STY a,X @ 9C
	{mne => 'STZ', modes => ['a']},
		
	# instead of CPY d,x @ D4
	{mne => 'PEI', modes => ['(d)']},

	# instead of CPY a,x @ DC
	{mne => 'JML', modes => ['(a)']},

	# instead of CPX d,x @ F4
	{mne => 'PEA', modes => ['a']},

	# instead of CPX a,x @ FC
	{mne => 'JSR', modes => ['(a,x)']}

);


print "\t\t.p816\n";
print "\n\n";
print "\t\t.zeropage\n";
print "ZLAB1:\t\t.res\t1\n";
print "\t\t.data\n";
print "LAB1:\t\t.res\t1\n";
print "\t\t.segment \"FDATA\"\n";
print "LABF:\t\t.res\t1\n";
print "\t\t.code\n";
print "\t\t.a8\n";
print "\t\t.i8\n";
doops("all");
print "\t\t.a16\n";
doops("m16");
print "\t\t.i16\n";
doops("x16");

sub doops($) {
	my ($mode) = @_;
	print "; modes = $mode\n";

	foreach my $op (@opcodes) {
		foreach my $m (@{$op->{modes}}) {
			my $mm = $modes{$m};
			if (exists $mm->{$mode}) {
				my @opers = @{$mm->{$mode}};
				foreach my $oo (@opers) {
					print "\t\t$op->{mne}\t$oo\n";
				}
			}
		}
	}
}


#
#my @all = map { @{$_->{modes}} } @opcodes;
#
#print join("\n", sort(unique(@all)));
#
#sub unique(@) {
#	my %seen = ();
#	return grep { ! $seen{$_} ++ } @_;
#}