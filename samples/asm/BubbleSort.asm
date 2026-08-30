; =============================================================================
; S-CPU SAMPLE - BUBBLE SORT
; =============================================================================
; What it does:
;   Copies a ROM array into RAM and sorts it in ascending order in place.
; Expected result:
;   After HALT, `sortedNumbers` contains 0 through 9 in the debugger RAM view.
; Runs on:
;   Desktop/CLI simulators and all S-CPU hardware implementations.
; Demonstrates:
;   Nested loops, arrays, pointer arithmetic, indirect access, and swapping.
; =============================================================================

#bank userpage
; #res counts 16-bit RAM words. The label difference is the number of elements
; in the ROM source array, so the RAM destination has exactly the same length.
sortedNumbers: #res numberCount - unsortedNumbers

#bank prg
start:
    call copyNumbersToRam
    
    ;for(x = 0; x < num - 1; x++){       
    ;    for(y = 0; y < num - x - 1; y++){          
    ;        if(arr[y] > arr[y + 1]){               
    ;            temp = arr[y];
    ;            arr[y] = arr[y + 1];
    ;            arr[y + 1] = temp;
    ;        }
    ;    }

    mov R0, #0          ; R0: x=0
    dec numberCount   ; A=(numberCount - 1)
    sta R1              ; R1: num=(numberCount - 1)

    outerLoop:
        mov R2, #0  ; R2: y=0
        lda R1
        sub R0
        sta R6      ; R6: num-x-1

        innerLoop:
            lda #(sortedNumbers)
            add R2          
            sta R3      ; R3=&arr[y]
            inc R3
            sta R4      ; R4=&arr[y+1]

            lda @(R3)  ; arr[y]
            sub @(R4)  ; arr[y+1]
            jz .break  ; arr[y]==arr[y+1]
            lsl
            jcs .break   ; arr[y] < arr[y+1]
            ; here:  arr[y] > arr[y+1]
            mov R5, @(R3)       ; R5=arr[y]  
            mov @(R3), @(R4)    ; arr[y]=arr[y+1]
            mov @(R4), R5       ; arr[y+1]=R5
            .break:

        inc R2
        sta R2
        sub R6
        jnz innerLoop

    inc R0
    sta R0
    sub R1
    jnz outerLoop

    halt

unsortedNumbers: #d16 6, 9, 3, 8, 0, 4, 2, 5, 7, 1
numberCount: #d16 numberCount - unsortedNumbers

copyNumbersToRam:
  mov R0, #0
  .loop:
    lda #(unsortedNumbers)
    add R0        
    sta R1          ; &unsortedNumbers[R0]
    lda @(R1)       ; A=unsortedNumbers[R0]
    sta R2          ; R2=unsortedNumbers[R0]

    lda #(sortedNumbers)
    add R0
    sta R1          ; &sortedNumbers[R0]

    mov @(R1), R2   ; sortedNumbers[R0] = number[R0]

    inc R0
    sta R0          ; R0++

    sub numberCount
    jnz .loop       ; jump .loop if R0 < numberCount

  ret
