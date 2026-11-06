#include "rental_native.h"
/* Whole KZT, exact integer arithmetic; no allocation or owned handles. */
int32_t __cdecl rental_service_fee(int32_t days) {
    if (days < 1 || days > 366) return -1;
    return days * 250;
}
