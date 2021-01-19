//
//  MasterDetailExampleAppUITests.swift
//  MasterDetailExampleAppUITests
//
//  Created by Sam Tang on 11/4/19.
//  Copyright © 2019 Sam Tang. All rights reserved.
//

import XCTest

class MasterDetailExampleAppUITests: XCTestCase {

    var application: XCUIApplication!
    
    override func setUp() {
        // Put setup code here. This method is called before the invocation of each test method in the class.

        // In UI tests it is usually best to stop immediately when a failure occurs.
        continueAfterFailure = false

        // UI tests must launch the application that they test. Doing this in setup will make sure it happens for each test method.
        application = XCUIApplication()
        application.launch()

        // In UI tests it’s important to set the initial state - such as interface orientation - required for your tests before they run. The setUp method is a good place to do this.
    }

    override func tearDown() {
        // Put teardown code here. This method is called after the invocation of each test method in the class.
    }

    func testEvents() {
        // Use recording to get started writing UI tests.
        application.navigationBars["Master"].buttons["Add"].tap()
        application.tables.cells.element.tap()
        application.navigationBars.buttons.element(boundBy: 0).tap()
        
        // Use XCTAssert and related functions to verify your tests produce the correct results.
    }

}
