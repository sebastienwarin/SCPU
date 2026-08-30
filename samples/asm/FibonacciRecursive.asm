; =============================================================================
; S-CPU SAMPLE - RECURSIVE FIBONACCI
; =============================================================================
; What it does:
;   Computes the first 12 Fibonacci values using the recursive definition.
; Expected result:
;   After HALT, `fibonacciValues` contains 0, 1, 1, 2, 3, 5, 8, 13, 21, 34, 55, 89.
; Runs on:
;   Desktop/CLI simulators and all S-CPU hardware implementations. At 1 kHz,
;   this intentionally slow version takes about 140 seconds.
; Demonstrates:
;   Recursion, stack arguments/results, and O(2^n) time versus O(n) stack use.
; =============================================================================

#const FIBONACCI_COUNT = 12

#bank userpage
; Reserve one 16-bit RAM word for each of the 12 results.
fibonacciValues: #res FIBONACCI_COUNT

#bank prg
start:
  mov R1, #0          ; R1: i=0

  outerLoop:
    push #0
    push R1
    call fibonacci
    pop     ; R1
    pop R0  ; result returned by fibonacci

    lda #(fibonacciValues)
    add R1
    sta R2
    mov @(R2), R0

    inc R1
    sta R1  ; i++
    sub #(FIBONACCI_COUNT)  ; i != nbr
    jnz outerLoop

  halt

fibonacci: ; int fibonacci(int n)
  ;   if (n == 0 || n == 1)
  ;     return n;
  ;   else
  ;     return (fibonacci(n-1) + fibonacci(n-2));

  lds #2, R3     ; R3=n
  jz .break      ; break if (n==0)
  lda R3         ; R3=n
  dec            ;
  jz .break      ; break if (n==1)

  push #0        ; fibonacciValues container
  lda R3         ; load n
  sub #1         ; A=n-1
  push           ; push arg (n-1)
  call fibonacci ; call function
  pop            ; pop arg
  pop R4         ; R4=fibonacci(n-1)

  lds #2, R3     ; R3=n
  push R4        ; save fibonacci(n-1)
  push #0        ; fibonacciValues container
  lda R3         ; load n
  sub #2         ; A=n-2
  push           ; push arg (n-2)
  call fibonacci ; call function
  pop            ; pop arg
  pop R4         ; R4=fibonacci(n-2)
  pop            ; A=fibonacci(n-1)

  add R4         ; A=(fibonacci(n-1) + fibonacci(n-2))
  sts #3         ; store the sum in the caller's result container
  ret            ; return

  .break:
  lds #2         ; A=n
  sts #3         ; store n in the caller's result container
  ret            ; return
