#!/usr/bin/perl -w
use strict;

my $skip = 0;
my $output = "";

if (@ARGV < 1)
{
	print STDERR "usage: ./prune_resx.pl <directory>";
	exit 1;
}

opendir(DIR, $ARGV[0]);
my @files = readdir(DIR);
closedir(DIR);

foreach my $file (@files)
{
if ($file =~ m/\.[a-z][a-z]-[A-Z][A-Z]\.resx/i)
{
$output = "";
open(RESX, "<$file");
while(<RESX>)
{
	if ($skip > 0)
	{
		if ($_ =~ m/\<\/data\>/)
		{
			$skip = 0;
		}
		next;
	}
	if (m/\<data name=\"(.*?)\"/)
	{
		if ($1 !~ m/\.(Text|Items\d+)$/)
		{
			$skip = 1;
			next;
		}
	}
	$output .= $_;
}
close(RESX);

open(RESX, ">$file");
print RESX $output;
close(RESX);
}
}
