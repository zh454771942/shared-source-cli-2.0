// ==++==
//
//   
//    Copyright (c) 2006 Microsoft Corporation.  All rights reserved.
//   
//    The use and distribution terms for this software are contained in the file
//    named license.txt, which can be found in the root of this distribution.
//    By using this software in any fashion, you are agreeing to be bound by the
//    terms of this license.
//   
//    You must not remove this notice, or any other, from this software.
//   
//
// ==--==
.assembly volatileexe{}
.class public _volatile{
	.field public static native int PTR
.method public static int32 main(class [mscorlib]System.String[]) {
.entrypoint
.locals	()
.maxstack	2
	ldc.i4	0xFFAAFFAA
	stsfld	native int _volatile::PTR
	ldsflda	native int _volatile::PTR
	volatile.
	ldind.i
	ldc.i4	0xFFAAFFAA
	bne.un	FAIL
PASS:
	ldc.i4	0x0000
	ret
FAIL:
	ldc.i4	0x0001
	ret
}
}
