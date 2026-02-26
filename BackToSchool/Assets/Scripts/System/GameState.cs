public enum GameState
{
    // --- 공통 상태 (Day 1-5) ---
    Subway,           // 1. 지하철 (메신저 씬)
    Morning_Slippers, // 2. 등교 (실내화 체크)
    Morning_Assembly, // 3. 조례 (스토리 씬)

    // --- 1~4일차 상태 ---
    Class_Intro_1,    // 4. 오전 수업 인트로
    Class_Minigame_1, // 4. 오전 수업 미니게임
    Class_Outro_1,    // 4. 오전 수업 아웃트로
    Lunch_Run,        // 5. 급식실 달리기
    Lunch_Tetris,     // 5. 위장 테트리스
    Lunch_FreeTime,   // 5. 점심시간
    Class_Intro_2,    // 6. 오후 수업 인트로
    Class_Minigame_2, // 6. 오후 수업 미니게임
    Class_Outro_2,    // 6. 오후 수업 아웃트로
    Closing_Assembly, // 7. 종례 (Day 1-4)
    AfterSchool,      // 8. 방과후
    GoHome,           // 9. 하교 (다음 날로)

    // --- 5일차 (금요일) 상태 ---
    Day5_BigCleaning,     // 4. 대청소 (바닥 쓸기)
    Day5_LockerCleaning,  // 5. 사물함 정리
    Day5_BagPacking,      // 6. 가방 싸기 (퍼즐)
    Day5_FreeTime,        // 7. 자유시간
    Day5_ClosingAssembly, // 8. 종례 (Day 5)
    Day5_LunchChoice,     // 9. 점심 선택 (스토리 씬)
    Day5_EndingCredits    // 10. 엔딩 크레딧
}
