; =============================================================================
; S-CPU SAMPLE - ITERATIVE FIBONACCI
; =============================================================================
; What it does:
;   Computes the first 12 Fibonacci values with an iterative algorithm.
; Expected result:
;   After HALT, `fibonacciValues` contains 0, 1, 1, 2, 3, 5, 8, 13, 21, 34, 55, 89.
; Runs on:
;   Desktop/CLI simulators and all S-CPU hardware implementations. At 1 kHz,
;   this version takes about 5 seconds.
; Demonstrates:
;   Loops and an O(n)-time, O(1)-working-space algorithm.
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
;    if (n <= 1) return n;
;    int curr = 0;
;    int prev1 = 1;
;    int prev2 = 0;
;    for (int i = 2; i <= n; i++) {
;        curr = prev1 + prev2;
;        prev2 = prev1;
;        prev1 = curr;
;    }
;    return curr;
;}

  lds #2, R3     ; R3=n
  jz .break      ; break if (n==0)
  lda R3         ; R3=n
  dec            ;
  jz .break      ; break if (n==1)

  mov R4, #0     ; curr = 0
  mov R5, #1     ; prev1 = 1
  mov R6, #0     ; prev2 = 0
  
  ; n > 1 here. One iteration computes the next value, so the first completed
  ; step corresponds to i=2.
  mov R7, #1     ; previous completed index
  .loop:
    lda R5
    add R6
    sta R4       ; curr = prev1 + prev2;
    mov R6, R5   ; prev2 = prev1;
    mov R5, R4   ; prev1 = curr;

    inc R7
    sta R7       ; advance to the value just computed
    sub R3       ; continue until that index reaches n
    jnz .loop

  sts #3, R4     ; store R4 in the caller's result container
  ret            ; return curr

  .break:
  lds #2         ; A=n
  sts #3         ; store n in the caller's result container
  ret            ; return
